# Player Statistics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Statistike tab's fake players with real top scorers + assisters per season, ingested from FootApi into MongoDB and read back from the DB.

**Architecture:** A new FootApi `best-players` provider call; a `PlayerSeasonStat` Mongo collection; an admin `PlayerStatsSyncService`; a public read controller; the Statistike tab reworked with a season selector and Gol/Ast sort toggle.

**Tech Stack:** .NET 10 (controllers, MongoDB.Driver, `IProviderRequestPacer`, xUnit), React 19 + TS + Tailwind, vitest.

**Reference spec:** `docs/superpowers/specs/2026-06-26-player-stats-design.md`

**Test/build:** backend `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter <Name> -p:UseAppHost=false`; frontend `cd frontend && npm test -- <Name>`.

**Conventions:** tests under `backend/PLeagueHub.Api.Tests/` and `frontend/src/**/*.test.*` are git-ignored — commit production only. Build/run backend with `-p:UseAppHost=false`. FootApi `best-players` field names are an **assumption** (SofaScore-style); the implementer confirms the exact `tournamentId` and shape with one live call (quota is available) before relying on Task 1's parse, and adjusts the private records + the Task-1 test if different.

---

## File Structure

**Backend create:** `Services/Football/FootballPlayerStat.cs`,
`Models/PlayerSeasonStatDocument.cs`,
`Services/Football/PlayerStatsSyncException.cs`,
`Responses/PlayerStatsSyncResponse.cs`,
`Services/Football/PlayerStatsSyncService.cs` (+ interface),
`Responses/PlayerStatDto.cs`, `Controllers/PlayerStatsController.cs`; tests
`PlayerStatsProviderTests.cs`, `PlayerStatsSyncServiceTests.cs`,
`PlayerStatsControllerTests.cs`.

**Backend modify:** `Services/Football/IFootballProvider.cs`,
`Services/Football/FootApiClient.cs`, `Configuration/MongoDbSettings.cs`,
`Data/MongoContext.cs`, `Data/MongoIndexInitializer.cs`,
`Controllers/IntegrationsController.cs`, `Program.cs`; the five test fakes that
implement `IFootballProvider` (`TeamSyncServiceTests.cs`,
`TeamLogoSyncServiceTests.cs`, `IntegrationEndpointsTests.cs`,
`MatchSyncServiceTests.cs`, `MatchDetailServiceTests.cs`).

**Frontend create:** `services/playerStatsApi.ts`; tests
`services/playerStatsApi.test.ts`, `pages/Stats.test.tsx`.

**Frontend modify:** `types/api.ts`, `pages/Stats.tsx`.

---

## Task 1: Provider — best-players

**Files:**
- Create: `backend/PLeagueHub.Api/Services/Football/FootballPlayerStat.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/FootApiClient.cs`
- Modify (fakes): `TeamSyncServiceTests.cs`, `TeamLogoSyncServiceTests.cs`, `IntegrationEndpointsTests.cs`, `MatchSyncServiceTests.cs`, `MatchDetailServiceTests.cs`
- Test: `backend/PLeagueHub.Api.Tests/PlayerStatsProviderTests.cs`

- [ ] **Step 1: Create the record** — `FootballPlayerStat.cs`:

```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed record FootballPlayerStat(
    int ProviderId,
    string Name,
    int TeamId,
    string TeamName,
    int Goals,
    int Assists,
    int Appearances);
```

- [ ] **Step 2: Extend the interface** — add to `IFootballProvider.cs`:

```csharp
    Task<IReadOnlyCollection<FootballPlayerStat>> GetBestPlayersAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Write the failing test** — create `PlayerStatsProviderTests.cs`:

```csharp
using System.Net;
using System.Text;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class PlayerStatsProviderTests
{
    [Fact]
    public async Task GetBestPlayersAsync_MergesGoalsAndAssistsByPlayer()
    {
        var client = CreateClient(JsonResponse("""
            {"topPlayers":{
              "goals":[
                {"player":{"id":1,"name":"Salah"},"team":{"id":44,"name":"Liverpool"},
                 "statistics":{"goals":29,"appearances":38}}],
              "assists":[
                {"player":{"id":1,"name":"Salah"},"team":{"id":44,"name":"Liverpool"},
                 "statistics":{"assists":18,"appearances":38}},
                {"player":{"id":2,"name":"Saka"},"team":{"id":42,"name":"Arsenal"},
                 "statistics":{"assists":10,"appearances":35}}]}}
            """));

        var players = await client.GetBestPlayersAsync(17, 61627);

        Assert.Equal(2, players.Count);
        var salah = players.Single(p => p.ProviderId == 1);
        Assert.Equal("Salah", salah.Name);
        Assert.Equal("Liverpool", salah.TeamName);
        Assert.Equal(29, salah.Goals);
        Assert.Equal(18, salah.Assists);
        Assert.Equal(38, salah.Appearances);
        var saka = players.Single(p => p.ProviderId == 2);
        Assert.Equal(0, saka.Goals);
        Assert.Equal(10, saka.Assists);
    }

    [Fact]
    public async Task GetBestPlayersAsync_ReturnsEmpty_OnNoContent()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent));
        Assert.Empty(await client.GetBestPlayersAsync(17, 61627));
    }

    private static FootApiClient CreateClient(HttpResponseMessage response)
    {
        var handler = new StubHandler(response);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://footapi7.p.rapidapi.com") };
        var settings = Microsoft.Extensions.Options.Options.Create(new PLeagueHub.Api.Configuration.FootApiSettings
        {
            ApiKey = "test-key",
            Host = "footapi7.p.rapidapi.com",
            BaseUrl = "https://footapi7.p.rapidapi.com"
        });
        return new FootApiClient(httpClient, settings);
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter PlayerStatsProviderTests -p:UseAppHost=false`
Expected: FAIL — `GetBestPlayersAsync` doesn't exist (compile error).

- [ ] **Step 5: Implement in FootApiClient** — add the method (after `GetMatchLineupsAsync`) and the private records/helper (next to the other private records) in `FootApiClient.cs`:

```csharp
    public async Task<IReadOnlyCollection<FootballPlayerStat>> GetBestPlayersAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest($"/api/tournament/{tournamentId}/season/{seasonId}/best-players");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<FootApiBestPlayersResponse>(
            responseStream, JsonOptions, cancellationToken);

        var players = new Dictionary<int, PlayerAccumulator>();
        Accumulate(payload?.TopPlayers?.Goals, players, isGoals: true);
        Accumulate(payload?.TopPlayers?.Assists, players, isGoals: false);

        return players.Values
            .Select(player => new FootballPlayerStat(
                player.Id, player.Name, player.TeamId, player.TeamName,
                player.Goals, player.Assists, player.Appearances))
            .ToArray();
    }

    private static void Accumulate(
        IReadOnlyCollection<FootApiPlayerEntry>? entries,
        Dictionary<int, PlayerAccumulator> players,
        bool isGoals)
    {
        foreach (var entry in entries ?? [])
        {
            if (entry.Player is null)
            {
                continue;
            }

            if (!players.TryGetValue(entry.Player.Id, out var accumulator))
            {
                accumulator = new PlayerAccumulator { Id = entry.Player.Id };
                players[entry.Player.Id] = accumulator;
            }

            if (string.IsNullOrEmpty(accumulator.Name))
            {
                accumulator.Name = entry.Player.Name ?? string.Empty;
            }

            if (entry.Team is not null && string.IsNullOrEmpty(accumulator.TeamName))
            {
                accumulator.TeamId = entry.Team.Id;
                accumulator.TeamName = entry.Team.Name ?? string.Empty;
            }

            if (entry.Statistics is not null)
            {
                if (isGoals && entry.Statistics.Goals is int goals)
                {
                    accumulator.Goals = goals;
                }

                if (!isGoals && entry.Statistics.Assists is int assists)
                {
                    accumulator.Assists = assists;
                }

                if (entry.Statistics.Appearances is int appearances && accumulator.Appearances == 0)
                {
                    accumulator.Appearances = appearances;
                }
            }
        }
    }

    private sealed class PlayerAccumulator
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Appearances { get; set; }
    }

    private sealed record FootApiBestPlayersResponse(FootApiTopPlayers? TopPlayers);
    private sealed record FootApiTopPlayers(
        IReadOnlyCollection<FootApiPlayerEntry>? Goals,
        IReadOnlyCollection<FootApiPlayerEntry>? Assists);
    private sealed record FootApiPlayerEntry(FootApiPlayerRef? Player, FootApiTeamRef? Team, FootApiPlayerStatistics? Statistics);
    private sealed record FootApiPlayerRef(int Id, string? Name);
    private sealed record FootApiTeamRef(int Id, string? Name);
    private sealed record FootApiPlayerStatistics(int? Goals, int? Assists, int? Appearances);
```

- [ ] **Step 6: Add the method to the five test fakes** — in each `Fake...Provider : IFootballProvider` (`TeamSyncServiceTests.cs`, `TeamLogoSyncServiceTests.cs`, `IntegrationEndpointsTests.cs`, `MatchSyncServiceTests.cs`, `MatchDetailServiceTests.cs`) add:

```csharp
        public Task<IReadOnlyCollection<FootballPlayerStat>> GetBestPlayersAsync(
            int tournamentId, int seasonId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<FootballPlayerStat>>([]);
```

(In `MatchSyncServiceTests` / `MatchDetailServiceTests` the fakes use compact `(int t, int s, ...)` signatures — match the surrounding style; the method body is the same.)

- [ ] **Step 7: Run the test**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter PlayerStatsProviderTests -p:UseAppHost=false`
Expected: PASS (2 tests; project compiles → all fakes implement the member).

- [ ] **Step 8: Commit**

```bash
git add backend/PLeagueHub.Api/Services/Football/FootballPlayerStat.cs backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs backend/PLeagueHub.Api/Services/Football/FootApiClient.cs
git commit -m "Add FootApi best-players (top scorers and assisters)"
```

---

## Task 2: PlayerSeasonStat collection

**Files:**
- Create: `backend/PLeagueHub.Api/Models/PlayerSeasonStatDocument.cs`
- Modify: `backend/PLeagueHub.Api/Configuration/MongoDbSettings.cs`
- Modify: `backend/PLeagueHub.Api/Data/MongoContext.cs`
- Modify: `backend/PLeagueHub.Api/Data/MongoIndexInitializer.cs`

- [ ] **Step 1: Create the document** — `PlayerSeasonStatDocument.cs`:

```csharp
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class PlayerSeasonStatDocument : BaseDocument
{
    [BsonElement("sezona")]
    public string Sezona { get; set; } = string.Empty;

    [BsonElement("provider_id")]
    public int ProviderId { get; set; }

    [BsonElement("ime")]
    public string Ime { get; set; } = string.Empty;

    [BsonElement("team_naziv")]
    public string TeamNaziv { get; set; } = string.Empty;

    [BsonElement("team_logo")]
    public string TeamLogoUrl { get; set; } = string.Empty;

    [BsonElement("golovi")]
    public int Golovi { get; set; }

    [BsonElement("asistencije")]
    public int Asistencije { get; set; }

    [BsonElement("odigrano")]
    public int Odigrano { get; set; }
}
```

- [ ] **Step 2: Add the collection name** — in `MongoDbSettings.cs`, after `MatchDetailsCollectionName`:

```csharp
    public string PlayerSeasonStatsCollectionName { get; init; } = "PlayerSeasonStats";
```

- [ ] **Step 3: Register the collection** — in `MongoContext.cs`: add to the constructor (after the `MatchDetails` line):

```csharp
        PlayerSeasonStats = Database.GetCollection<PlayerSeasonStatDocument>(settings.PlayerSeasonStatsCollectionName);
```

add the property (after the `MatchDetails` property):

```csharp
    public IMongoCollection<PlayerSeasonStatDocument> PlayerSeasonStats { get; }
```

and the switch case in `GetCollection<TDocument>()` (after the `MatchDetailDocument` case):

```csharp
            var type when type == typeof(PlayerSeasonStatDocument) => (IMongoCollection<TDocument>)PlayerSeasonStats,
```

- [ ] **Step 4: Add an index** — in `MongoIndexInitializer.cs`: call it in `EnsureIndexesAsync` (after `CreateMatchDetailIndexesAsync`):

```csharp
        await CreatePlayerSeasonStatIndexesAsync(cancellationToken);
```

and add the method (next to `CreateMatchDetailIndexesAsync`):

```csharp
    private async Task CreatePlayerSeasonStatIndexesAsync(CancellationToken cancellationToken)
    {
        var index = new CreateIndexModel<PlayerSeasonStatDocument>(
            Builders<PlayerSeasonStatDocument>.IndexKeys
                .Ascending(stat => stat.Sezona)
                .Descending(stat => stat.Golovi),
            new CreateIndexOptions { Name = "idx_playerSeasonStats_sezona_golovi" });

        await _context.PlayerSeasonStats.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
    }
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj -p:UseAppHost=false`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Models/PlayerSeasonStatDocument.cs backend/PLeagueHub.Api/Configuration/MongoDbSettings.cs backend/PLeagueHub.Api/Data/MongoContext.cs backend/PLeagueHub.Api/Data/MongoIndexInitializer.cs
git commit -m "Add PlayerSeasonStat collection"
```

---

## Task 3: PlayerStatsSyncService

**Files:**
- Create: `backend/PLeagueHub.Api/Responses/PlayerStatsSyncResponse.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/PlayerStatsSyncException.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/PlayerStatsSyncService.cs`
- Test: `backend/PLeagueHub.Api.Tests/PlayerStatsSyncServiceTests.cs`

- [ ] **Step 1: Add DTO + exception**

`PlayerStatsSyncResponse.cs`:
```csharp
namespace PLeagueHub.Api.Responses;

public sealed record PlayerStatsSyncResponse(int Total, int Created, int Updated);
```

`PlayerStatsSyncException.cs`:
```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed class PlayerStatsSyncException : Exception
{
    public PlayerStatsSyncException(string message)
        : base(message)
    {
    }

    public PlayerStatsSyncException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [ ] **Step 2: Write the failing test** — create `PlayerStatsSyncServiceTests.cs`:

```csharp
using System.Linq.Expressions;
using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class PlayerStatsSyncServiceTests
{
    [Fact]
    public async Task SyncSeasonAsync_CreatesStatsWithNormalizedSeasonAndTeamInfo()
    {
        var provider = new FakeProvider
        {
            Seasons = [new FootballSeason(61627, "Premier League 24/25", "24/25")],
            Players = [new FootballPlayerStat(1, "Salah", 44, "Liverpool FC", 29, 18, 38)]
        };
        var teams = new FakeTeamRepo([new Team { Id = "t44", ProviderId = 44, Naziv = "Liverpool", LogoUrl = "/liv.png" }]);
        var stats = new FakeStatRepo();
        var service = new PlayerStatsSyncService(provider, teams, stats, new NoopPacer());

        var result = await service.SyncSeasonAsync(61627, CancellationToken.None);

        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Created);
        var doc = Assert.Single(stats.Items);
        Assert.Equal("2024/25", doc.Sezona);
        Assert.Equal(1, doc.ProviderId);
        Assert.Equal("Salah", doc.Ime);
        Assert.Equal("Liverpool", doc.TeamNaziv);   // resolved from Team doc by provider id
        Assert.Equal("/liv.png", doc.TeamLogoUrl);
        Assert.Equal(29, doc.Golovi);
        Assert.Equal(18, doc.Asistencije);
    }

    [Fact]
    public async Task SyncSeasonAsync_IsIdempotent()
    {
        var provider = new FakeProvider
        {
            Seasons = [new FootballSeason(61627, "PL", "24/25")],
            Players = [new FootballPlayerStat(1, "Salah", 44, "Liverpool", 29, 18, 38)]
        };
        var stats = new FakeStatRepo();
        var service = new PlayerStatsSyncService(provider, new FakeTeamRepo([]), stats, new NoopPacer());

        await service.SyncSeasonAsync(61627, CancellationToken.None);
        var second = await service.SyncSeasonAsync(61627, CancellationToken.None);

        Assert.Single(stats.Items);
        Assert.Equal(0, second.Created);
        Assert.Equal(1, second.Updated);
    }

    [Fact]
    public async Task SyncSeasonAsync_WrapsProviderFailure()
    {
        var provider = new FakeProvider
        {
            Seasons = [new FootballSeason(61627, "PL", "24/25")],
            ThrowOnPlayers = true
        };
        var service = new PlayerStatsSyncService(provider, new FakeTeamRepo([]), new FakeStatRepo(), new NoopPacer());

        await Assert.ThrowsAsync<PlayerStatsSyncException>(
            () => service.SyncSeasonAsync(61627, CancellationToken.None));
    }

    [Fact]
    public async Task SyncSeasonAsync_Throws_WhenSeasonNotFound()
    {
        var provider = new FakeProvider { Seasons = [] };
        var service = new PlayerStatsSyncService(provider, new FakeTeamRepo([]), new FakeStatRepo(), new NoopPacer());

        await Assert.ThrowsAsync<PlayerStatsSyncException>(
            () => service.SyncSeasonAsync(61627, CancellationToken.None));
    }

    private sealed class FakeProvider : IFootballProvider
    {
        public IReadOnlyCollection<FootballSeason> Seasons { get; set; } = [];
        public IReadOnlyCollection<FootballPlayerStat> Players { get; set; } = [];
        public bool ThrowOnPlayers { get; set; }

        public Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(int tournamentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Seasons);
        public Task<IReadOnlyCollection<FootballPlayerStat>> GetBestPlayersAsync(int tournamentId, int seasonId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnPlayers) throw new HttpRequestException("boom");
            return Task.FromResult(Players);
        }

        public Task<JsonDocument> SearchAsync(string term, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(int t, int s, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FootballTeamLogo> GetTeamLogoAsync(int p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FootballEvent>> GetSeasonEventsAsync(int t, int s, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FootballStatItem>> GetMatchStatisticsAsync(int e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FootballIncident>> GetMatchIncidentsAsync(int e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FootballLineups?> GetMatchLineupsAsync(int e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTeamRepo(IEnumerable<Team> teams) : IRepository<Team>
    {
        private readonly List<Team> _teams = teams.ToList();
        public Task<IReadOnlyCollection<Team>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<Team>>(_teams);
        public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Team?> FindOneAsync(Expression<Func<Team, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Expression<Func<Team, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Team> CreateAsync(Team e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(string id, Team e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeStatRepo : IRepository<PlayerSeasonStatDocument>
    {
        public List<PlayerSeasonStatDocument> Items { get; } = [];
        private int _seq = 1;
        public Task<IReadOnlyCollection<PlayerSeasonStatDocument>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<PlayerSeasonStatDocument>>(Items.ToList());
        public Task<PlayerSeasonStatDocument> CreateAsync(PlayerSeasonStatDocument e, CancellationToken cancellationToken = default)
        {
            e.Id = $"stat-{_seq++}";
            Items.Add(e);
            return Task.FromResult(e);
        }
        public Task<bool> UpdateAsync(string id, PlayerSeasonStatDocument e, CancellationToken cancellationToken = default)
        {
            var index = Items.FindIndex(item => item.Id == id);
            if (index >= 0) Items[index] = e;
            return Task.FromResult(index >= 0);
        }
        public Task<PlayerSeasonStatDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PlayerSeasonStatDocument?> FindOneAsync(Expression<Func<PlayerSeasonStatDocument, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Expression<Func<PlayerSeasonStatDocument, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoopPacer : IProviderRequestPacer
    {
        public Task WaitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter PlayerStatsSyncServiceTests -p:UseAppHost=false`
Expected: FAIL — `PlayerStatsSyncService` does not exist.

- [ ] **Step 4: Implement the service** — create `PlayerStatsSyncService.cs`:

```csharp
using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IPlayerStatsSyncService
{
    Task<PlayerStatsSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken);
}

public sealed class PlayerStatsSyncService : IPlayerStatsSyncService
{
    private const int PremierLeagueTournamentId = 17;

    private readonly IFootballProvider _provider;
    private readonly IRepository<Team> _teams;
    private readonly IRepository<PlayerSeasonStatDocument> _stats;
    private readonly IProviderRequestPacer _pacer;

    public PlayerStatsSyncService(
        IFootballProvider provider,
        IRepository<Team> teams,
        IRepository<PlayerSeasonStatDocument> stats,
        IProviderRequestPacer pacer)
    {
        _provider = provider;
        _teams = teams;
        _stats = stats;
        _pacer = pacer;
    }

    public async Task<PlayerStatsSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<FootballSeason> seasons;
        IReadOnlyCollection<FootballPlayerStat> players;

        try
        {
            seasons = await _provider.GetSeasonsAsync(PremierLeagueTournamentId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new PlayerStatsSyncException("FootAPI seasons could not be loaded.", exception);
        }

        var season = seasons.FirstOrDefault(item => item.Id == seasonId)
            ?? throw new PlayerStatsSyncException($"Season {seasonId} was not found.");
        var label = MatchSyncService.NormalizeSeasonLabel(season.Year);

        try
        {
            await _pacer.WaitAsync(cancellationToken);
            players = await _provider.GetBestPlayersAsync(PremierLeagueTournamentId, seasonId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new PlayerStatsSyncException("FootAPI best players could not be loaded.", exception);
        }

        var teamByProvider = (await _teams.GetAllAsync(cancellationToken))
            .Where(team => team.ProviderId is not null)
            .GroupBy(team => team.ProviderId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var existing = (await _stats.GetAllAsync(cancellationToken))
            .Where(stat => string.Equals(stat.Sezona, label, StringComparison.Ordinal))
            .GroupBy(stat => stat.ProviderId)
            .ToDictionary(group => group.Key, group => group.First());

        var created = 0;
        var updated = 0;

        foreach (var player in players)
        {
            teamByProvider.TryGetValue(player.TeamId, out var team);
            var naziv = team?.Naziv ?? player.TeamName;
            var logo = team?.LogoUrl ?? string.Empty;

            if (existing.TryGetValue(player.ProviderId, out var current))
            {
                current.Ime = player.Name;
                current.TeamNaziv = naziv;
                current.TeamLogoUrl = logo;
                current.Golovi = player.Goals;
                current.Asistencije = player.Assists;
                current.Odigrano = player.Appearances;
                await _stats.UpdateAsync(current.Id, current, cancellationToken);
                updated++;
            }
            else
            {
                var document = new PlayerSeasonStatDocument
                {
                    Sezona = label,
                    ProviderId = player.ProviderId,
                    Ime = player.Name,
                    TeamNaziv = naziv,
                    TeamLogoUrl = logo,
                    Golovi = player.Goals,
                    Asistencije = player.Assists,
                    Odigrano = player.Appearances
                };
                var createdDoc = await _stats.CreateAsync(document, cancellationToken);
                existing[player.ProviderId] = createdDoc;
                created++;
            }
        }

        return new PlayerStatsSyncResponse(players.Count, created, updated);
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter PlayerStatsSyncServiceTests -p:UseAppHost=false`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Responses/PlayerStatsSyncResponse.cs backend/PLeagueHub.Api/Services/Football/PlayerStatsSyncException.cs backend/PLeagueHub.Api/Services/Football/PlayerStatsSyncService.cs
git commit -m "Add player stats sync service"
```

---

## Task 4: Admin endpoint + DI

**Files:**
- Modify: `backend/PLeagueHub.Api/Controllers/IntegrationsController.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test: `backend/PLeagueHub.Api.Tests/PlayerStatsEndpointTests.cs`

- [ ] **Step 1: Write the failing test** — create `PlayerStatsEndpointTests.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Controllers;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class PlayerStatsEndpointTests
{
    [Fact]
    public async Task SyncPlayerStats_ReturnsCounts()
    {
        var sync = new FakePlayerStatsSync { Result = new PlayerStatsSyncResponse(20, 18, 2) };
        var controller = new IntegrationsController(null!, null!, null!, sync);

        var result = await controller.SyncPlayerStatsAsync(61627, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<PlayerStatsSyncResponse>(ok.Value);
        Assert.Equal(61627, sync.RequestedSeasonId);
    }

    [Fact]
    public async Task SyncPlayerStats_Returns502_OnFailure()
    {
        var sync = new FakePlayerStatsSync { Throw = true };
        var controller = new IntegrationsController(null!, null!, null!, sync);

        var result = await controller.SyncPlayerStatsAsync(61627, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
    }

    private sealed class FakePlayerStatsSync : IPlayerStatsSyncService
    {
        public PlayerStatsSyncResponse Result { get; set; } = new(0, 0, 0);
        public bool Throw { get; set; }
        public int? RequestedSeasonId { get; private set; }

        public Task<PlayerStatsSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken)
        {
            RequestedSeasonId = seasonId;
            if (Throw) throw new PlayerStatsSyncException("down");
            return Task.FromResult(Result);
        }
    }
}
```

> **Note:** `new IntegrationsController(null!, null!, null!, sync)` passes nulls for the existing `TeamSyncService`, `TeamLogoSyncService`, `IMatchSyncService` — the new endpoint never touches them. Confirm the constructor parameter order in Step 2 (existing three first, then `IPlayerStatsSyncService`).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter PlayerStatsEndpointTests -p:UseAppHost=false`
Expected: FAIL — controller has no `IPlayerStatsSyncService` param / `SyncPlayerStatsAsync`.

- [ ] **Step 3: Add the dependency + endpoint** — in `IntegrationsController.cs`, add the field + constructor parameter (append `IPlayerStatsSyncService playerStatsSyncService` last) and the endpoint method:

field + ctor (extend the existing block):
```csharp
    private readonly IPlayerStatsSyncService _playerStatsSyncService;
```
add the parameter `IPlayerStatsSyncService playerStatsSyncService` to the constructor signature (after `IMatchSyncService matchSyncService`) and `_playerStatsSyncService = playerStatsSyncService;` to its body.

endpoint (inside the class):
```csharp
    [HttpPost("sync/player-stats")]
    [ProducesResponseType(typeof(PlayerStatsSyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PlayerStatsSyncResponse>> SyncPlayerStatsAsync(
        [FromQuery] int seasonId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _playerStatsSyncService.SyncSeasonAsync(seasonId, cancellationToken);
            return Ok(result);
        }
        catch (PlayerStatsSyncException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
    }
```

- [ ] **Step 4: Register the service** — in `Program.cs`, next to `builder.Services.AddScoped<IMatchSyncService, MatchSyncService>();` add:

```csharp
builder.Services.AddScoped<IPlayerStatsSyncService, PlayerStatsSyncService>();
```

- [ ] **Step 5: Run tests + build**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter PlayerStatsEndpointTests -p:UseAppHost=false`
Expected: PASS (2 tests).
Run: `dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj -p:UseAppHost=false`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Controllers/IntegrationsController.cs backend/PLeagueHub.Api/Program.cs
git commit -m "Expose admin player stats sync endpoint"
```

---

## Task 5: Public read endpoint

**Files:**
- Create: `backend/PLeagueHub.Api/Responses/PlayerStatDto.cs`
- Create: `backend/PLeagueHub.Api/Controllers/PlayerStatsController.cs`
- Test: `backend/PLeagueHub.Api.Tests/PlayerStatsReadTests.cs`

- [ ] **Step 1: Add the DTO** — `PlayerStatDto.cs`:

```csharp
namespace PLeagueHub.Api.Responses;

public sealed record PlayerStatDto(
    int Position,
    int ProviderId,
    string Ime,
    string TeamNaziv,
    string TeamLogoUrl,
    int Golovi,
    int Asistencije,
    int Odigrano);
```

- [ ] **Step 2: Write the failing test** — create `PlayerStatsReadTests.cs`:

```csharp
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Controllers;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Tests;

public sealed class PlayerStatsReadTests
{
    [Fact]
    public async Task GetPlayerStats_OrdersByGoalsThenAssists_ForSeason()
    {
        var repo = new FakeStatRepo(
        [
            new() { Sezona = "2024/25", ProviderId = 2, Ime = "Saka", Golovi = 10, Asistencije = 12 },
            new() { Sezona = "2024/25", ProviderId = 1, Ime = "Salah", Golovi = 29, Asistencije = 18 },
            new() { Sezona = "2023/24", ProviderId = 3, Ime = "Other", Golovi = 99, Asistencije = 0 }
        ]);
        var controller = new PlayerStatsController(repo);

        var result = await controller.GetAsync("2024/25", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyCollection<PlayerStatDto>>(ok.Value);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Salah", rows.First().Ime);
        Assert.Equal(1, rows.First().Position);
        Assert.Equal("Saka", rows.Last().Ime);
    }

    [Fact]
    public async Task GetPlayerStats_ReturnsEmpty_ForBlankSeason()
    {
        var controller = new PlayerStatsController(new FakeStatRepo([]));
        var result = await controller.GetAsync(null, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyCollection<PlayerStatDto>>(ok.Value));
    }

    private sealed class FakeStatRepo(IEnumerable<PlayerSeasonStatDocument> items) : IRepository<PlayerSeasonStatDocument>
    {
        private readonly List<PlayerSeasonStatDocument> _items = items.ToList();
        public Task<IReadOnlyCollection<PlayerSeasonStatDocument>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<PlayerSeasonStatDocument>>(_items);
        public Task<PlayerSeasonStatDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PlayerSeasonStatDocument?> FindOneAsync(Expression<Func<PlayerSeasonStatDocument, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Expression<Func<PlayerSeasonStatDocument, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PlayerSeasonStatDocument> CreateAsync(PlayerSeasonStatDocument e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(string id, PlayerSeasonStatDocument e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter PlayerStatsReadTests -p:UseAppHost=false`
Expected: FAIL — `PlayerStatsController` does not exist.

- [ ] **Step 4: Implement the controller** — create `PlayerStatsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/player-stats")]
public sealed class PlayerStatsController : ControllerBase
{
    private readonly IRepository<PlayerSeasonStatDocument> _stats;

    public PlayerStatsController(IRepository<PlayerSeasonStatDocument> stats)
    {
        _stats = stats;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PlayerStatDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PlayerStatDto>>> GetAsync(
        [FromQuery] string? season,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(season))
        {
            return Ok(Array.Empty<PlayerStatDto>());
        }

        var all = await _stats.GetAllAsync(cancellationToken);

        var rows = all
            .Where(stat => string.Equals(stat.Sezona, season, StringComparison.Ordinal))
            .OrderByDescending(stat => stat.Golovi)
            .ThenByDescending(stat => stat.Asistencije)
            .Select((stat, index) => new PlayerStatDto(
                index + 1,
                stat.ProviderId,
                stat.Ime,
                stat.TeamNaziv,
                stat.TeamLogoUrl,
                stat.Golovi,
                stat.Asistencije,
                stat.Odigrano))
            .ToArray();

        return Ok(rows);
    }
}
```

- [ ] **Step 5: Run tests**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter PlayerStatsReadTests -p:UseAppHost=false`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Responses/PlayerStatDto.cs backend/PLeagueHub.Api/Controllers/PlayerStatsController.cs
git commit -m "Expose public player stats read endpoint"
```

---

## Task 6: Frontend types + api

**Files:**
- Modify: `frontend/src/types/api.ts`
- Create: `frontend/src/services/playerStatsApi.ts`
- Test: `frontend/src/services/playerStatsApi.test.ts`

- [ ] **Step 1: Add the type** — append to `types/api.ts`:

```ts
export interface PlayerStat {
  position: number;
  providerId: number;
  ime: string;
  teamNaziv: string;
  teamLogoUrl: string;
  golovi: number;
  asistencije: number;
  odigrano: number;
}
```

- [ ] **Step 2: Write the failing test** — create `services/playerStatsApi.test.ts`:

```ts
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { get } = vi.hoisted(() => ({ get: vi.fn().mockResolvedValue({ data: [] }) }));
vi.mock('./api', () => ({ api: { get } }));

import { playerStatsApi } from './playerStatsApi';

describe('playerStatsApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('requests player stats for a season', () => {
    playerStatsApi.get('2024/25');
    expect(get).toHaveBeenCalledWith('/api/player-stats', { params: { season: '2024/25' } });
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd frontend && npm test -- playerStatsApi`
Expected: FAIL — cannot find `./playerStatsApi`.

- [ ] **Step 4: Implement** — create `services/playerStatsApi.ts`:

```ts
import type { PlayerStat } from '../types/api';
import { api } from './api';

export const playerStatsApi = {
  async get(season: string) {
    const response = await api.get<PlayerStat[]>('/api/player-stats', {
      params: { season }
    });
    return response.data;
  }
};
```

- [ ] **Step 5: Run the test**

Run: `cd frontend && npm test -- playerStatsApi`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/src/types/api.ts frontend/src/services/playerStatsApi.ts
git commit -m "Add player stats API client and type"
```

---

## Task 7: Statistike tab rework

**Files:**
- Modify: `frontend/src/pages/Stats.tsx`
- Test: `frontend/src/pages/Stats.test.tsx`

- [ ] **Step 1: Write the failing test** — create `pages/Stats.test.tsx`:

```tsx
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getStats, getSeasons } = vi.hoisted(() => ({ getStats: vi.fn(), getSeasons: vi.fn() }));
vi.mock('../services/playerStatsApi', () => ({ playerStatsApi: { get: getStats } }));
vi.mock('../services/standingsApi', () => ({ standingsApi: { getSeasons } }));

import { Stats } from './Stats';

const rows = [
  { position: 1, providerId: 1, ime: 'Salah', teamNaziv: 'Liverpool', teamLogoUrl: '', golovi: 29, asistencije: 8, odigrano: 38 },
  { position: 2, providerId: 2, ime: 'Saka', teamNaziv: 'Arsenal', teamLogoUrl: '', golovi: 10, asistencije: 14, odigrano: 35 }
];

describe('Stats page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getSeasons.mockResolvedValue([{ season: '2024/25' }, { season: '2023/24' }]);
    getStats.mockResolvedValue(rows);
  });

  it('lists players for the newest season ordered by goals', async () => {
    render(<Stats />);
    expect(await screen.findByText('Salah')).toBeInTheDocument();
    expect(screen.getByText('Saka')).toBeInTheDocument();
    expect(getStats).toHaveBeenCalledWith('2024/25');
  });

  it('reorders by assists when the Ast toggle is used', async () => {
    render(<Stats />);
    await screen.findByText('Salah');

    await userEvent.click(screen.getByRole('button', { name: 'Ast' }));

    const tableRows = screen.getAllByRole('row');
    // header row is [0]; first data row is [1]
    expect(within(tableRows[1]).getByText('Saka')).toBeInTheDocument();
  });

  it('filters by search text', async () => {
    render(<Stats />);
    await screen.findByText('Salah');

    await userEvent.type(screen.getByPlaceholderText('Pretrazi igraca'), 'saka');

    expect(screen.queryByText('Salah')).toBeNull();
    expect(screen.getByText('Saka')).toBeInTheDocument();
  });

  it('shows an empty state when there is no data', async () => {
    getStats.mockResolvedValue([]);
    render(<Stats />);
    expect(await screen.findByText(/nema statistike za izabranu sezonu/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- Stats`
Expected: FAIL — current `Stats` uses `playersApi`/`teamsApi`, not the mocked modules.

- [ ] **Step 3: Rework the page** — replace the whole contents of `pages/Stats.tsx`:

```tsx
import { Search, TrendingUp } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { playerStatsApi } from '../services/playerStatsApi';
import { standingsApi } from '../services/standingsApi';
import type { PlayerStat, Season } from '../types/api';
import { TeamLogo } from '../components/TeamLogo';

type SortKey = 'golovi' | 'asistencije';

export function Stats() {
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [season, setSeason] = useState('');
  const [players, setPlayers] = useState<PlayerStat[]>([]);
  const [search, setSearch] = useState('');
  const [sortKey, setSortKey] = useState<SortKey>('golovi');
  const [status, setStatus] = useState<'loading' | 'ready'>('loading');

  useEffect(() => {
    standingsApi.getSeasons().then(setSeasons).catch(() => setSeasons([]));
  }, []);

  const selectedSeason = season || seasons[0]?.season || '';

  useEffect(() => {
    if (!selectedSeason) {
      setPlayers([]);
      setStatus('ready');
      return;
    }

    setStatus('loading');
    playerStatsApi.get(selectedSeason)
      .then((data) => {
        setPlayers(data);
        setStatus('ready');
      })
      .catch(() => {
        setPlayers([]);
        setStatus('ready');
      });
  }, [selectedSeason]);

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    return players
      .filter((player) => (term ? player.ime.toLowerCase().includes(term) : true))
      .slice()
      .sort((a, b) => b[sortKey] - a[sortKey]);
  }, [players, search, sortKey]);

  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="flex items-center gap-1 text-[10px] font-bold uppercase text-brand">
              <TrendingUp size={13} /> Najbolji igraci
            </p>
            <h1 className="mt-1 text-xl font-extrabold">Statistike igraca</h1>
          </div>
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
            <label className="relative">
              <Search className="absolute left-3 top-2.5 text-slate-400" size={15} />
              <input
                className="w-full rounded-md border border-slate-300 py-2 pl-9 pr-3 text-sm outline-none focus:border-brand"
                placeholder="Pretrazi igraca"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            </label>
            <select
              aria-label="Sezona"
              className="rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-brand"
              value={selectedSeason}
              onChange={(event) => setSeason(event.target.value)}
            >
              {seasons.map((item) => (
                <option key={item.season} value={item.season}>{item.season}</option>
              ))}
            </select>
            <div className="flex overflow-hidden rounded-md border border-slate-300 text-sm font-semibold">
              <button
                className={`px-3 py-2 ${sortKey === 'golovi' ? 'bg-brand text-white' : 'text-slate-600'}`}
                onClick={() => setSortKey('golovi')}
                type="button"
              >
                Gol
              </button>
              <button
                className={`px-3 py-2 ${sortKey === 'asistencije' ? 'bg-brand text-white' : 'text-slate-600'}`}
                onClick={() => setSortKey('asistencije')}
                type="button"
              >
                Ast
              </button>
            </div>
          </div>
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        {status === 'ready' && visible.length === 0 ? (
          <p className="px-4 py-8 text-center text-sm text-slate-400">Nema statistike za izabranu sezonu.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-left text-sm">
              <thead className="bg-ink text-[10px] uppercase text-slate-300">
                <tr>
                  <th className="px-4 py-3">#</th>
                  <th className="px-4 py-3">Igrac</th>
                  <th className="px-4 py-3">Tim</th>
                  <th className="px-4 py-3 text-right">Gol</th>
                  <th className="px-4 py-3 text-right">Ast</th>
                </tr>
              </thead>
              <tbody>
                {visible.map((player, index) => (
                  <tr key={player.providerId} className="border-b border-slate-100 hover:bg-slate-50">
                    <td className="px-4 py-3 text-xs font-bold text-slate-400">{index + 1}</td>
                    <td className="px-4 py-3 font-bold">{player.ime}</td>
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-2">
                        <TeamLogo className="size-6" logoUrl={player.teamLogoUrl} name={player.teamNaziv} />
                        <span className="text-xs font-semibold">{player.teamNaziv}</span>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-right font-black">{player.golovi}</td>
                    <td className="px-4 py-3 text-right font-black">{player.asistencije}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
```

- [ ] **Step 4: Run the tests + full suite + build**

Run: `cd frontend && npm test -- Stats`
Expected: PASS (4 tests).
Run: `cd frontend && npm run build`
Expected: tsc + vite succeed.
Run: `cd frontend && npm test`
Expected: all suites pass.

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/Stats.tsx
git commit -m "Rework Statistike tab to real player stats by season"
```

---

## Final Verification (verification-before-completion)

- [ ] Backend: `dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj -p:UseAppHost=false` → 0 errors; `dotnet test ... --filter "PlayerStats" -p:UseAppHost=false` → green.
- [ ] Frontend: `cd frontend && npm test` → green; `npm run build` → succeeds.
- [ ] **Assumption check (do this first, before trusting Task 1):** with the API running, confirm the live shape and tournament id, e.g. via a temporary `DebugController` (added then removed) hitting `/api/tournament/17/season/61627/best-players` and `/api/tournament/1/season/61627/best-players`. Confirm the JSON keys (`topPlayers`/`goals`/`assists`/`player.id`/`statistics.goals` etc.) and which `tournamentId` returns data; adjust `FootApiClient`'s `best-players` parse records + `PlayerStatsSyncService.PremierLeagueTournamentId` + the Task-1 test if different.
- [ ] **Live ingestion (quota permitting):** `POST /api/integrations/football/sync/player-stats?seasonId=61627` (and 76986, 52186…); then `GET /api/player-stats?season=2024/25` returns the ordered list; the Statistike tab shows it.

## Out of Scope (v1)
Ratings/clean-sheets tabs, player profiles, per-90 metrics, auto-refresh.
