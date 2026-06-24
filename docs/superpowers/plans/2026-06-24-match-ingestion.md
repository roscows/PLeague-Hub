# Match Ingestion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An admin-triggered, idempotent sync that pulls all Premier League matches (per season or all 35 seasons) from FootApi into MongoDB, upserting teams as needed and storing each match's FootApi event id.

**Architecture:** A new `GetSeasonEventsAsync` on the football provider pages FootApi fixtures; a `MatchSyncService` maps events to `Match` docs (upserting `Team` docs by provider id, idempotent by match provider id); an admin endpoint on `IntegrationsController` triggers it.

**Tech Stack:** .NET 10 (ASP.NET Core controllers, MongoDB.Driver, xUnit), `IProviderRequestPacer` for rate limiting.

**Reference spec:** `docs/superpowers/specs/2026-06-24-match-ingestion-design.md`

**Test command:** `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter <Name>` (all unit tests below need no MongoDB).

**Convention notes:**
- Test files live under `backend/PLeagueHub.Api.Tests/` which is **git-ignored** — they stay local, never committed. Commit only production files.
- All 4 fake `IFootballProvider` implementations in the test project must gain any new interface member or the test project won't compile.

---

## File Structure

**Create:**
- `Services/Football/FootballEvent.cs` — provider event record.
- `Services/Football/MatchSyncException.cs` — provider-failure signal → 502.
- `Services/Football/MatchSyncService.cs` + `IMatchSyncService` — events→matches, team upsert, idempotent upsert.
- `Responses/MatchSyncResponse.cs` — sync counts DTO.
- Tests: `MatchSyncServiceTests.cs`, `MatchIngestionEndpointTests.cs`.

**Modify:**
- `Services/Football/IFootballProvider.cs` — add `GetSeasonEventsAsync`.
- `Services/Football/FootApiClient.cs` — implement `GetSeasonEventsAsync` (paged) + parse records.
- `Models/Match.cs` — add `ProviderId`.
- `Data/MongoIndexInitializer.cs:71-89` — add `provider_id` index.
- `Controllers/IntegrationsController.cs` — add `sync/matches` endpoint + inject `IMatchSyncService`.
- `Program.cs:61-62` — register `IMatchSyncService`.
- Test fakes gaining `GetSeasonEventsAsync`: `StandingsServiceTests.cs`, `TeamSyncServiceTests.cs`, `TeamLogoSyncServiceTests.cs`, `IntegrationEndpointsTests.cs`.

---

## Task 1: Provider — fetch season events (paged)

**Files:**
- Create: `backend/PLeagueHub.Api/Services/Football/FootballEvent.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/FootApiClient.cs`
- Modify (fakes): `StandingsServiceTests.cs`, `TeamSyncServiceTests.cs`, `TeamLogoSyncServiceTests.cs`, `IntegrationEndpointsTests.cs`
- Test: `backend/PLeagueHub.Api.Tests/FootApiClientTests.cs`

- [ ] **Step 1: Write the failing test** — append to `FootApiClientTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task GetSeasonEventsAsync_ParsesLastAndNextPages()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/events/last/0"))
            {
                return JsonResponse("""
                    {"hasNextPage":false,"events":[
                      {"id":111,"roundInfo":{"round":1},
                       "homeTeam":{"id":42,"name":"Arsenal","nameCode":"ARS"},
                       "awayTeam":{"id":44,"name":"Liverpool","nameCode":"LIV"},
                       "homeScore":{"current":2},"awayScore":{"current":1},
                       "status":{"type":"finished"},"startTimestamp":1660000000}]}
                    """);
            }

            if (path.EndsWith("/events/next/0"))
            {
                return JsonResponse("""
                    {"hasNextPage":false,"events":[
                      {"id":222,"roundInfo":{"round":2},
                       "homeTeam":{"id":42,"name":"Arsenal","nameCode":"ARS"},
                       "awayTeam":{"id":60,"name":"City","nameCode":"MCI"},
                       "homeScore":{"current":null},"awayScore":{"current":null},
                       "status":{"type":"notstarted"},"startTimestamp":1660500000}]}
                    """);
            }

            return JsonResponse("""{"hasNextPage":false,"events":[]}""");
        });
        var client = CreateClient(handler, apiKey: "test-key");

        var events = await client.GetSeasonEventsAsync(17, 76986);

        Assert.Equal(2, events.Count);
        var finished = events.Single(item => item.EventId == 111);
        Assert.Equal(1, finished.Round);
        Assert.Equal(42, finished.HomeTeamId);
        Assert.Equal("Arsenal", finished.HomeTeamName);
        Assert.Equal("LIV", finished.AwayTeamCode);
        Assert.Equal(2, finished.HomeScore);
        Assert.Equal("finished", finished.StatusType);
        Assert.Equal(1660000000, finished.StartTimestamp);
        var upcoming = events.Single(item => item.EventId == 222);
        Assert.Null(upcoming.HomeScore);
        Assert.Equal("notstarted", upcoming.StatusType);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter GetSeasonEventsAsync_ParsesLastAndNextPages`
Expected: FAIL — `FootballEvent` / `GetSeasonEventsAsync` don't exist (compile error).

- [ ] **Step 3: Create the event record** — `FootballEvent.cs`:

```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed record FootballEvent(
    int EventId,
    int Round,
    int HomeTeamId,
    string HomeTeamName,
    string HomeTeamCode,
    int AwayTeamId,
    string AwayTeamName,
    string AwayTeamCode,
    int? HomeScore,
    int? AwayScore,
    string StatusType,
    long StartTimestamp);
```

- [ ] **Step 4: Extend the interface** — add to `IFootballProvider.cs` (inside the interface):

```csharp
    Task<IReadOnlyCollection<FootballEvent>> GetSeasonEventsAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 5: Implement in FootApiClient** — add `using System.Net;` at the top of `FootApiClient.cs` (next to the existing usings), then add this method after `GetSeasonsAsync` and the records next to the existing private records:

```csharp
    public async Task<IReadOnlyCollection<FootballEvent>> GetSeasonEventsAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        var events = new Dictionary<int, FootballEvent>();

        foreach (var bucket in new[] { "last", "next" })
        {
            for (var page = 0; ; page++)
            {
                using var request = CreateRequest(
                    $"/api/tournament/{tournamentId}/season/{seasonId}/events/{bucket}/{page}");
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    break;
                }

                response.EnsureSuccessStatusCode();

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<FootApiEventsResponse>(
                    responseStream,
                    JsonOptions,
                    cancellationToken);

                var rows = payload?.Events ?? [];

                foreach (var row in rows.Where(item => item.HomeTeam is not null && item.AwayTeam is not null))
                {
                    events[row.Id] = new FootballEvent(
                        row.Id,
                        row.RoundInfo?.Round ?? 0,
                        row.HomeTeam!.Id,
                        row.HomeTeam.Name ?? string.Empty,
                        row.HomeTeam.NameCode ?? string.Empty,
                        row.AwayTeam!.Id,
                        row.AwayTeam.Name ?? string.Empty,
                        row.AwayTeam.NameCode ?? string.Empty,
                        row.HomeScore?.Current,
                        row.AwayScore?.Current,
                        row.Status?.Type ?? string.Empty,
                        row.StartTimestamp);
                }

                if (rows.Count == 0 || payload?.HasNextPage != true)
                {
                    break;
                }
            }
        }

        return events.Values.ToArray();
    }
```

```csharp
    private sealed record FootApiEventsResponse(
        bool? HasNextPage,
        IReadOnlyCollection<FootApiEvent>? Events);

    private sealed record FootApiEvent(
        int Id,
        FootApiRoundInfo? RoundInfo,
        FootApiEventTeam? HomeTeam,
        FootApiEventTeam? AwayTeam,
        FootApiScore? HomeScore,
        FootApiScore? AwayScore,
        FootApiStatus? Status,
        long StartTimestamp);

    private sealed record FootApiRoundInfo(int Round);

    private sealed record FootApiEventTeam(int Id, string? Name, string? NameCode);

    private sealed record FootApiScore(int? Current);

    private sealed record FootApiStatus(string? Type);
```

- [ ] **Step 6: Add the method to all four test fakes** — in each of `StandingsServiceTests.cs`, `TeamSyncServiceTests.cs`, `TeamLogoSyncServiceTests.cs`, `IntegrationEndpointsTests.cs`, find the `private sealed class Fake...Provider : IFootballProvider` and add:

```csharp
        public Task<IReadOnlyCollection<FootballEvent>> GetSeasonEventsAsync(
            int tournamentId, int seasonId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<FootballEvent>>([]);
```

- [ ] **Step 7: Run the client test + confirm project compiles**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter GetSeasonEventsAsync_ParsesLastAndNextPages`
Expected: PASS (compiles → all fakes implement the member).

- [ ] **Step 8: Commit (production only)**

```bash
git add backend/PLeagueHub.Api/Services/Football/FootballEvent.cs backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs backend/PLeagueHub.Api/Services/Football/FootApiClient.cs
git commit -m "Add FootApi season events fetch"
```

---

## Task 2: Match model provider id + index

**Files:**
- Modify: `backend/PLeagueHub.Api/Models/Match.cs`
- Modify: `backend/PLeagueHub.Api/Data/MongoIndexInitializer.cs:71-89`

- [ ] **Step 1: Add the field** — in `Match.cs`, add the `using` if missing (`MongoDB.Bson.Serialization.Attributes` is already imported) and add the property after `ZavrsenaAt`:

```csharp
    [BsonElement("provider_id")]
    [BsonIgnoreIfNull]
    public int? ProviderId { get; set; }
```

- [ ] **Step 2: Add the index** — in `MongoIndexInitializer.cs`, extend the `indexes` array inside `CreateMatchIndexesAsync` (currently lines 73-86) by adding a fourth entry:

```csharp
            new CreateIndexModel<Match>(
                Builders<Match>.IndexKeys.Ascending(match => match.ProviderId),
                new CreateIndexOptions { Name = "idx_matches_provider_id", Sparse = true })
```

(Non-unique + sparse: idempotency is enforced by the service, and seeded matches without a provider id are skipped by the sparse index.)

- [ ] **Step 3: Build to verify**

Run: `dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add backend/PLeagueHub.Api/Models/Match.cs backend/PLeagueHub.Api/Data/MongoIndexInitializer.cs
git commit -m "Add provider id to Match with lookup index"
```

---

## Task 3: MatchSyncService

**Files:**
- Create: `backend/PLeagueHub.Api/Responses/MatchSyncResponse.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/MatchSyncException.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/MatchSyncService.cs`
- Test: `backend/PLeagueHub.Api.Tests/MatchSyncServiceTests.cs`

- [ ] **Step 1: Add the DTO and exception**

`MatchSyncResponse.cs`:
```csharp
namespace PLeagueHub.Api.Responses;

public sealed record MatchSyncResponse(
    int Total,
    int Created,
    int Updated,
    int TeamsCreated);
```

`MatchSyncException.cs`:
```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed class MatchSyncException : Exception
{
    public MatchSyncException(string message)
        : base(message)
    {
    }

    public MatchSyncException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [ ] **Step 2: Write the failing test** — create `MatchSyncServiceTests.cs`:

```csharp
using System.Linq.Expressions;
using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class MatchSyncServiceTests
{
    private static FootballEvent Event(int id, int round, int homeId, int awayId, int? hs, int? as_, string status)
        => new(id, round, homeId, $"Team{homeId}", $"T{homeId}", awayId, $"Team{awayId}", $"T{awayId}", hs, as_, status, 1_660_000_000);

    [Fact]
    public async Task SyncSeasonAsync_CreatesTeamsAndMatch_WithMappedFields()
    {
        var provider = new FakeProvider
        {
            Seasons = [new FootballSeason(76986, "Premier League 24/25", "24/25")],
            EventsBySeason = { [76986] = [Event(111, 1, 42, 44, 2, 1, "finished")] }
        };
        var teams = new FakeTeamRepository([]);
        var matches = new FakeMatchRepository([]);
        var service = new MatchSyncService(provider, teams, matches, new NoopPacer());

        var result = await service.SyncSeasonAsync(76986, CancellationToken.None);

        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Created);
        Assert.Equal(2, result.TeamsCreated);
        var match = Assert.Single(matches.Items);
        Assert.Equal(111, match.ProviderId);
        Assert.Equal(1, match.Kolo);
        Assert.Equal("2024/25", match.Sezona);
        Assert.Equal("zavrsena", match.Status);
        Assert.Equal(2, match.GolDomacin);
        Assert.Equal(1, match.GolGost);
        Assert.False(string.IsNullOrEmpty(match.DomacinId));
        Assert.NotEqual(match.DomacinId, match.GostId);
    }

    [Fact]
    public async Task SyncSeasonAsync_ReusesExistingTeamByProviderId()
    {
        var provider = new FakeProvider
        {
            Seasons = [new FootballSeason(76986, "PL", "24/25")],
            EventsBySeason = { [76986] = [Event(111, 1, 42, 44, 0, 0, "finished")] }
        };
        var teams = new FakeTeamRepository(
        [
            new Team { Id = "t42", ProviderId = 42, Naziv = "Arsenal" },
            new Team { Id = "t44", ProviderId = 44, Naziv = "Liverpool" }
        ]);
        var service = new MatchSyncService(provider, teams, new FakeMatchRepository([]), new NoopPacer());

        var result = await service.SyncSeasonAsync(76986, CancellationToken.None);

        Assert.Equal(0, result.TeamsCreated);
        Assert.Equal(2, teams.Items.Count);
    }

    [Fact]
    public async Task SyncSeasonAsync_IsIdempotentByProviderId()
    {
        var provider = new FakeProvider
        {
            Seasons = [new FootballSeason(76986, "PL", "24/25")],
            EventsBySeason = { [76986] = [Event(111, 1, 42, 44, 2, 1, "finished")] }
        };
        var teams = new FakeTeamRepository([]);
        var matches = new FakeMatchRepository([]);
        var service = new MatchSyncService(provider, teams, matches, new NoopPacer());

        await service.SyncSeasonAsync(76986, CancellationToken.None);
        var second = await service.SyncSeasonAsync(76986, CancellationToken.None);

        Assert.Single(matches.Items);
        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Updated);
    }

    [Fact]
    public async Task SyncAllSeasonsAsync_AggregatesAcrossSeasons()
    {
        var provider = new FakeProvider
        {
            Seasons =
            [
                new FootballSeason(1, "PL", "23/24"),
                new FootballSeason(2, "PL", "24/25")
            ],
            EventsBySeason =
            {
                [1] = [Event(11, 1, 42, 44, 1, 0, "finished")],
                [2] = [Event(22, 1, 42, 60, 0, 0, "finished")]
            }
        };
        var service = new MatchSyncService(
            provider, new FakeTeamRepository([]), new FakeMatchRepository([]), new NoopPacer());

        var result = await service.SyncAllSeasonsAsync(CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Created);
        Assert.Equal(3, result.TeamsCreated); // 42, 44, 60
    }

    [Fact]
    public async Task SyncSeasonAsync_WrapsProviderFailure()
    {
        var provider = new FakeProvider { ThrowOnEvents = true, Seasons = [new FootballSeason(76986, "PL", "24/25")] };
        var service = new MatchSyncService(
            provider, new FakeTeamRepository([]), new FakeMatchRepository([]), new NoopPacer());

        await Assert.ThrowsAsync<MatchSyncException>(
            () => service.SyncSeasonAsync(76986, CancellationToken.None));
    }

    private sealed class FakeProvider : IFootballProvider
    {
        public IReadOnlyCollection<FootballSeason> Seasons { get; set; } = [];
        public Dictionary<int, IReadOnlyCollection<FootballEvent>> EventsBySeason { get; } = new();
        public bool ThrowOnEvents { get; set; }

        public Task<JsonDocument> SearchAsync(string term, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<FootballTeamLogo> GetTeamLogoAsync(int providerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(
            int tournamentId, int seasonId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(
            int tournamentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Seasons);
        public Task<IReadOnlyCollection<FootballEvent>> GetSeasonEventsAsync(
            int tournamentId, int seasonId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnEvents)
            {
                throw new HttpRequestException("boom");
            }

            return Task.FromResult(EventsBySeason.GetValueOrDefault(seasonId, []));
        }
    }

    private sealed class FakeTeamRepository : IRepository<Team>
    {
        public List<Team> Items { get; }
        private int _seq = 1;
        public FakeTeamRepository(IEnumerable<Team> seed) => Items = seed.ToList();

        public Task<IReadOnlyCollection<Team>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<Team>>(Items.ToList());
        public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(t => t.Id == id));
        public Task<Team?> FindOneAsync(Expression<Func<Team, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(predicate.Compile()));
        public Task<bool> ExistsAsync(Expression<Func<Team, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(predicate.Compile()));
        public Task<Team> CreateAsync(Team entity, CancellationToken cancellationToken = default)
        {
            entity.Id = $"team-{_seq++}";
            Items.Add(entity);
            return Task.FromResult(entity);
        }
        public Task<bool> UpdateAsync(string id, Team entity, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class FakeMatchRepository : IRepository<Match>
    {
        public List<Match> Items { get; }
        private int _seq = 1;
        public FakeMatchRepository(IEnumerable<Match> seed) => Items = seed.ToList();

        public Task<IReadOnlyCollection<Match>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<Match>>(Items.ToList());
        public Task<Match?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(m => m.Id == id));
        public Task<Match?> FindOneAsync(Expression<Func<Match, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.FirstOrDefault(predicate.Compile()));
        public Task<bool> ExistsAsync(Expression<Func<Match, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(Items.Any(predicate.Compile()));
        public Task<Match> CreateAsync(Match entity, CancellationToken cancellationToken = default)
        {
            entity.Id = $"match-{_seq++}";
            Items.Add(entity);
            return Task.FromResult(entity);
        }
        public Task<bool> UpdateAsync(string id, Match entity, CancellationToken cancellationToken = default)
        {
            var index = Items.FindIndex(m => m.Id == id);
            if (index >= 0)
            {
                Items[index] = entity;
            }

            return Task.FromResult(index >= 0);
        }
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class NoopPacer : IProviderRequestPacer
    {
        public Task WaitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchSyncServiceTests`
Expected: FAIL — `MatchSyncService` does not exist.

- [ ] **Step 4: Implement the service** — create `MatchSyncService.cs`:

```csharp
using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IMatchSyncService
{
    Task<MatchSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken);

    Task<MatchSyncResponse> SyncAllSeasonsAsync(CancellationToken cancellationToken);
}

public sealed class MatchSyncService : IMatchSyncService
{
    private const int PremierLeagueTournamentId = 17;

    private readonly IFootballProvider _footballProvider;
    private readonly IRepository<Team> _teamsRepository;
    private readonly IRepository<Match> _matchesRepository;
    private readonly IProviderRequestPacer _requestPacer;

    public MatchSyncService(
        IFootballProvider footballProvider,
        IRepository<Team> teamsRepository,
        IRepository<Match> matchesRepository,
        IProviderRequestPacer requestPacer)
    {
        _footballProvider = footballProvider;
        _teamsRepository = teamsRepository;
        _matchesRepository = matchesRepository;
        _requestPacer = requestPacer;
    }

    public async Task<MatchSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken)
    {
        var seasons = await GetSeasonsAsync(cancellationToken);
        var season = seasons.FirstOrDefault(item => item.Id == seasonId)
            ?? throw new MatchSyncException($"Season {seasonId} was not found.");

        var teamCache = await BuildTeamCacheAsync(cancellationToken);
        var matchCache = await BuildMatchCacheAsync(cancellationToken);

        return await SyncSeasonCoreAsync(season, teamCache, matchCache, cancellationToken);
    }

    public async Task<MatchSyncResponse> SyncAllSeasonsAsync(CancellationToken cancellationToken)
    {
        var seasons = await GetSeasonsAsync(cancellationToken);
        var teamCache = await BuildTeamCacheAsync(cancellationToken);
        var matchCache = await BuildMatchCacheAsync(cancellationToken);

        var total = 0;
        var created = 0;
        var updated = 0;
        var teamsCreated = 0;

        foreach (var season in seasons)
        {
            var result = await SyncSeasonCoreAsync(season, teamCache, matchCache, cancellationToken);
            total += result.Total;
            created += result.Created;
            updated += result.Updated;
            teamsCreated += result.TeamsCreated;
        }

        return new MatchSyncResponse(total, created, updated, teamsCreated);
    }

    private async Task<MatchSyncResponse> SyncSeasonCoreAsync(
        FootballSeason season,
        Dictionary<int, Team> teamCache,
        Dictionary<int, Match> matchCache,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<FootballEvent> events;

        try
        {
            await _requestPacer.WaitAsync(cancellationToken);
            events = await _footballProvider.GetSeasonEventsAsync(
                PremierLeagueTournamentId,
                season.Id,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new MatchSyncException("FootAPI events could not be loaded.", exception);
        }

        var seasonLabel = NormalizeSeasonLabel(season.Year);
        var created = 0;
        var updated = 0;
        var teamsCreated = 0;

        foreach (var footballEvent in events)
        {
            var (homeId, homeCreated) = await EnsureTeamAsync(
                footballEvent.HomeTeamId, footballEvent.HomeTeamName, footballEvent.HomeTeamCode, teamCache, cancellationToken);
            var (awayId, awayCreated) = await EnsureTeamAsync(
                footballEvent.AwayTeamId, footballEvent.AwayTeamName, footballEvent.AwayTeamCode, teamCache, cancellationToken);

            if (homeCreated)
            {
                teamsCreated++;
            }

            if (awayCreated)
            {
                teamsCreated++;
            }

            if (matchCache.TryGetValue(footballEvent.EventId, out var existing))
            {
                ApplyEvent(existing, footballEvent, homeId, awayId, seasonLabel);
                await _matchesRepository.UpdateAsync(existing.Id, existing, cancellationToken);
                updated++;
            }
            else
            {
                var match = new Match { ProviderId = footballEvent.EventId };
                ApplyEvent(match, footballEvent, homeId, awayId, seasonLabel);
                var createdMatch = await _matchesRepository.CreateAsync(match, cancellationToken);
                matchCache[footballEvent.EventId] = createdMatch;
                created++;
            }
        }

        return new MatchSyncResponse(events.Count, created, updated, teamsCreated);
    }

    private async Task<(string TeamId, bool Created)> EnsureTeamAsync(
        int providerId,
        string name,
        string code,
        Dictionary<int, Team> teamCache,
        CancellationToken cancellationToken)
    {
        if (teamCache.TryGetValue(providerId, out var existing))
        {
            return (existing.Id, false);
        }

        var team = new Team
        {
            ProviderId = providerId,
            Naziv = name.Trim(),
            Skracenica = code.Trim(),
            Stadion = string.Empty,
            Osnovan = 0,
            LogoUrl = string.Empty,
            Bodovi = 0,
            Pozicija = 0
        };

        var createdTeam = await _teamsRepository.CreateAsync(team, cancellationToken);
        teamCache[providerId] = createdTeam;
        return (createdTeam.Id, true);
    }

    private static void ApplyEvent(Match match, FootballEvent footballEvent, string homeId, string awayId, string seasonLabel)
    {
        match.ProviderId = footballEvent.EventId;
        match.DomacinId = homeId;
        match.GostId = awayId;
        match.Kolo = footballEvent.Round;
        match.Sezona = seasonLabel;
        match.Datum = DateTimeOffset.FromUnixTimeSeconds(footballEvent.StartTimestamp).UtcDateTime;
        match.GolDomacin = footballEvent.HomeScore;
        match.GolGost = footballEvent.AwayScore;
        match.Status = MapStatus(footballEvent.StatusType);
    }

    private async Task<Dictionary<int, Team>> BuildTeamCacheAsync(CancellationToken cancellationToken)
    {
        var teams = await _teamsRepository.GetAllAsync(cancellationToken);
        var cache = new Dictionary<int, Team>();

        foreach (var team in teams.Where(item => item.ProviderId is not null))
        {
            cache.TryAdd(team.ProviderId!.Value, team);
        }

        return cache;
    }

    private async Task<Dictionary<int, Match>> BuildMatchCacheAsync(CancellationToken cancellationToken)
    {
        var matches = await _matchesRepository.GetAllAsync(cancellationToken);
        var cache = new Dictionary<int, Match>();

        foreach (var match in matches.Where(item => item.ProviderId is not null))
        {
            cache.TryAdd(match.ProviderId!.Value, match);
        }

        return cache;
    }

    private async Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _footballProvider.GetSeasonsAsync(PremierLeagueTournamentId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new MatchSyncException("FootAPI seasons could not be loaded.", exception);
        }
    }

    private static string MapStatus(string statusType)
    {
        return statusType.ToLowerInvariant() switch
        {
            "finished" => "zavrsena",
            "inprogress" => "uzivo",
            _ => "zakazana"
        };
    }

    public static string NormalizeSeasonLabel(string label)
    {
        var parts = label.Split('/');

        if (parts.Length != 2)
        {
            return label;
        }

        var start = parts[0].Trim();
        var end = parts[1].Trim();

        if (start.Length == 4 || start.Length != 2 || !int.TryParse(start, out var startYear))
        {
            return label;
        }

        var century = startYear >= 90 ? "19" : "20";
        return $"{century}{start}/{end}";
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchSyncServiceTests`
Expected: PASS (all 5).

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Responses/MatchSyncResponse.cs backend/PLeagueHub.Api/Services/Football/MatchSyncException.cs backend/PLeagueHub.Api/Services/Football/MatchSyncService.cs
git commit -m "Add match sync service with team upsert and idempotent matches"
```

---

## Task 4: Admin endpoint + DI

**Files:**
- Modify: `backend/PLeagueHub.Api/Controllers/IntegrationsController.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs:61-62`
- Test: `backend/PLeagueHub.Api.Tests/MatchIngestionEndpointTests.cs`

- [ ] **Step 1: Write the failing test** — create `MatchIngestionEndpointTests.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Controllers;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class MatchIngestionEndpointTests
{
    [Fact]
    public async Task SyncMatches_SingleSeason_ReturnsCounts()
    {
        var sync = new FakeMatchSyncService { Result = new MatchSyncResponse(10, 8, 2, 4) };
        var controller = CreateController(sync);

        var result = await controller.SyncMatchesAsync(seasonId: 76986, all: false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<MatchSyncResponse>(ok.Value);
        Assert.Equal(76986, sync.RequestedSeasonId);
        Assert.False(sync.AllRequested);
    }

    [Fact]
    public async Task SyncMatches_All_ReturnsCounts()
    {
        var sync = new FakeMatchSyncService { Result = new MatchSyncResponse(1, 1, 0, 0) };
        var controller = CreateController(sync);

        var result = await controller.SyncMatchesAsync(seasonId: null, all: true, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(sync.AllRequested);
    }

    [Fact]
    public async Task SyncMatches_Rejects_WhenNeitherProvided()
    {
        var controller = CreateController(new FakeMatchSyncService());

        var result = await controller.SyncMatchesAsync(seasonId: null, all: false, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SyncMatches_Rejects_WhenBothProvided()
    {
        var controller = CreateController(new FakeMatchSyncService());

        var result = await controller.SyncMatchesAsync(seasonId: 76986, all: true, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SyncMatches_Returns502_OnSyncFailure()
    {
        var sync = new FakeMatchSyncService { Throw = true };
        var controller = CreateController(sync);

        var result = await controller.SyncMatchesAsync(seasonId: 76986, all: false, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
    }

    private static IntegrationsController CreateController(IMatchSyncService matchSync)
        => new(null!, null!, matchSync);

    private sealed class FakeMatchSyncService : IMatchSyncService
    {
        public MatchSyncResponse Result { get; set; } = new(0, 0, 0, 0);
        public bool Throw { get; set; }
        public int? RequestedSeasonId { get; private set; }
        public bool AllRequested { get; private set; }

        public Task<MatchSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken)
        {
            RequestedSeasonId = seasonId;
            if (Throw)
            {
                throw new MatchSyncException("down");
            }

            return Task.FromResult(Result);
        }

        public Task<MatchSyncResponse> SyncAllSeasonsAsync(CancellationToken cancellationToken)
        {
            AllRequested = true;
            if (Throw)
            {
                throw new MatchSyncException("down");
            }

            return Task.FromResult(Result);
        }
    }
}
```

> **Note:** `CreateController(new(null!, null!, matchSync))` passes nulls for the two existing concrete services (`TeamSyncService`, `TeamLogoSyncService`) — the new endpoint never touches them, so the nulls are safe for these unit tests. Confirm the constructor parameter order in Step 3 matches.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchIngestionEndpointTests`
Expected: FAIL — `IntegrationsController` has no `IMatchSyncService` ctor param / `SyncMatchesAsync`.

- [ ] **Step 3: Add the dependency + endpoint** — in `IntegrationsController.cs`, add the field + constructor param, and the endpoint. Replace the existing fields/constructor block:

```csharp
    private readonly TeamSyncService _teamSyncService;
    private readonly TeamLogoSyncService _teamLogoSyncService;
    private readonly IMatchSyncService _matchSyncService;

    public IntegrationsController(
        TeamSyncService teamSyncService,
        TeamLogoSyncService teamLogoSyncService,
        IMatchSyncService matchSyncService)
    {
        _teamSyncService = teamSyncService;
        _teamLogoSyncService = teamLogoSyncService;
        _matchSyncService = matchSyncService;
    }
```

Add the endpoint method inside the class (after `SyncTeamLogosAsync`):

```csharp
    [HttpPost("sync/matches")]
    [ProducesResponseType(typeof(MatchSyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<MatchSyncResponse>> SyncMatchesAsync(
        [FromQuery] int? seasonId,
        [FromQuery] bool all = false,
        CancellationToken cancellationToken = default)
    {
        if (all == seasonId.HasValue)
        {
            return BadRequest(new { message = "Provide exactly one of seasonId or all=true." });
        }

        try
        {
            var result = all
                ? await _matchSyncService.SyncAllSeasonsAsync(cancellationToken)
                : await _matchSyncService.SyncSeasonAsync(seasonId!.Value, cancellationToken);

            return Ok(result);
        }
        catch (MatchSyncException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
    }
```

Add `using PLeagueHub.Api.Responses;` to the controller's usings if not present (it already imports `PLeagueHub.Api.Responses` and `PLeagueHub.Api.Services.Football`).

- [ ] **Step 4: Register the service** — in `Program.cs`, next to the existing `builder.Services.AddScoped<TeamSyncService>();` (line 61), add:

```csharp
builder.Services.AddScoped<IMatchSyncService, MatchSyncService>();
```

- [ ] **Step 5: Run the endpoint tests + full build**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchIngestionEndpointTests`
Expected: PASS (all 5).
Run: `dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Controllers/IntegrationsController.cs backend/PLeagueHub.Api/Program.cs
git commit -m "Expose admin match ingestion endpoint"
```

---

## Final Verification (verification-before-completion)

- [ ] `dotnet build backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj` → 0 errors.
- [ ] `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter "FootApiClientTests|MatchSyncServiceTests|MatchIngestionEndpointTests"` → all green (no MongoDB needed).
- [ ] **Live run (Docker Mongo + FootApi key, admin JWT):** `POST /api/integrations/football/sync/matches?seasonId=96668` → 200 with counts; `GET /api/matches?season=2026/27` shows ingested matches with provider ids; re-run the sync → `created:0, updated:N` (idempotent). Then optionally `?all=true` (long-running).
- [ ] **Assumption check:** confirm the live FootApi `events/last|next/{page}` response shape (`events[]`, `hasNextPage`, `roundInfo.round`, `status.type`, `startTimestamp`, `homeScore.current`). If different, adjust the private parse records in `FootApiClient`.

## Out of Scope (Feature B)
Match statistics/incidents/lineups and the clickable match detail page — separate spec/plan, depends on `Match.ProviderId`.
