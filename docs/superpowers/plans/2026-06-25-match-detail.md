# Match Detail + Statistics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every match row clickable, opening a Flashscore-style detail page (header, statistics, incident timeline, lineups) fetched lazily from FootApi by the stored match provider id and cached.

**Architecture:** Three new FootApi provider calls (`/api/match/{id}/statistics|incidents|lineups`); a cached `MatchDetailService` that combines a Mongo-built header with the lazily-fetched FootApi data; a public controller; a frontend detail page + clickable rows.

**Tech Stack:** .NET 10 (controllers, `IMemoryCache`, `IProviderRequestPacer`, xUnit), React 19 + TS + Tailwind, react-router-dom, vitest.

**Reference spec:** `docs/superpowers/specs/2026-06-25-match-detail-design.md`

**Test commands:** backend `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter <Name> -p:UseAppHost=false`; frontend `cd frontend && npm test -- <Name>`.

**Convention notes:**
- Tests under `backend/PLeagueHub.Api.Tests/` and `frontend/src/**/*.test.*` are **git-ignored** — commit production only.
- Build/run backend with `-p:UseAppHost=false` (csproj already sets `UseAppHost=false`).
- FootApi response field names are SofaScore-standard **assumptions**; verify on the first live run (after the daily quota resets) and adjust the private parse records if needed.

---

## File Structure

**Backend create:** `Services/Football/FootballMatchDetail.cs` (provider records),
`Services/Football/MatchDetailUnavailableException.cs`,
`Services/Football/MatchDetailService.cs` (+ `IMatchDetailService`),
`Responses/MatchDetailResponse.cs` (all detail DTOs),
`Controllers/MatchDetailController.cs`; tests `MatchDetailProviderTests.cs`,
`MatchDetailServiceTests.cs`, `MatchDetailControllerTests.cs`.

**Backend modify:** `Services/Football/IFootballProvider.cs`,
`Services/Football/FootApiClient.cs`, `Program.cs`; plus the five test fakes that
implement `IFootballProvider` (`StandingsServiceTests.cs`,
`TeamSyncServiceTests.cs`, `TeamLogoSyncServiceTests.cs`,
`IntegrationEndpointsTests.cs`, `MatchSyncServiceTests.cs`).

**Frontend create:** `services/matchDetailApi.ts`, `pages/MatchDetail.tsx`;
tests `services/matchDetailApi.test.ts`, `pages/MatchDetail.test.tsx`,
`components/MatchRow.test.tsx`.

**Frontend modify:** `types/api.ts`, `App.tsx`, `components/MatchRow.tsx`.

---

## Task 1: Provider — match statistics, incidents, lineups

**Files:**
- Create: `backend/PLeagueHub.Api/Services/Football/FootballMatchDetail.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/FootApiClient.cs`
- Modify (fakes): `StandingsServiceTests.cs`, `TeamSyncServiceTests.cs`, `TeamLogoSyncServiceTests.cs`, `IntegrationEndpointsTests.cs`, `MatchSyncServiceTests.cs`
- Test: `backend/PLeagueHub.Api.Tests/MatchDetailProviderTests.cs`

- [ ] **Step 1: Create the provider records** — `FootballMatchDetail.cs`:

```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed record FootballStatItem(string Name, string Home, string Away);

public sealed record FootballIncident(
    string Type,
    int Minute,
    bool IsHome,
    string PlayerName,
    string PlayerInName,
    string PlayerOutName,
    string Detail);

public sealed record FootballLineupPlayer(string Name, int Number, bool IsSubstitute, string Position);

public sealed record FootballLineupTeam(string Formation, IReadOnlyCollection<FootballLineupPlayer> Players);

public sealed record FootballLineups(bool Confirmed, FootballLineupTeam? Home, FootballLineupTeam? Away);
```

- [ ] **Step 2: Extend the interface** — add to `IFootballProvider.cs`:

```csharp
    Task<IReadOnlyCollection<FootballStatItem>> GetMatchStatisticsAsync(
        int eventId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FootballIncident>> GetMatchIncidentsAsync(
        int eventId,
        CancellationToken cancellationToken = default);

    Task<FootballLineups?> GetMatchLineupsAsync(
        int eventId,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Write the failing test** — create `MatchDetailProviderTests.cs`:

```csharp
using System.Net;
using System.Text;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class MatchDetailProviderTests
{
    [Fact]
    public async Task GetMatchStatisticsAsync_FlattensAllPeriodGroups()
    {
        var client = CreateClient(JsonResponse("""
            {"statistics":[
              {"period":"ALL","groups":[
                {"groupName":"Possession","statisticsItems":[
                  {"name":"Ball possession","home":"55%","away":"45%"}]},
                {"groupName":"Shots","statisticsItems":[
                  {"name":"Total shots","home":"14","away":"9"}]}]},
              {"period":"1ST","groups":[
                {"groupName":"X","statisticsItems":[{"name":"ignored","home":"1","away":"2"}]}]}]}
            """));

        var stats = await client.GetMatchStatisticsAsync(14025269);

        Assert.Equal(2, stats.Count);
        Assert.Contains(stats, s => s.Name == "Ball possession" && s.Home == "55%" && s.Away == "45%");
        Assert.Contains(stats, s => s.Name == "Total shots" && s.Home == "14" && s.Away == "9");
    }

    [Fact]
    public async Task GetMatchIncidentsAsync_MapsGoalsAndSubstitutions()
    {
        var client = CreateClient(JsonResponse("""
            {"incidents":[
              {"incidentType":"goal","time":23,"isHome":true,"player":{"name":"Saka"}},
              {"incidentType":"substitution","time":70,"isHome":false,
               "playerIn":{"name":"Jota"},"playerOut":{"name":"Nunez"}}]}
            """));

        var incidents = await client.GetMatchIncidentsAsync(14025269);

        Assert.Equal(2, incidents.Count);
        var goal = incidents.Single(i => i.Type == "goal");
        Assert.Equal(23, goal.Minute);
        Assert.True(goal.IsHome);
        Assert.Equal("Saka", goal.PlayerName);
        var sub = incidents.Single(i => i.Type == "substitution");
        Assert.Equal("Jota", sub.PlayerInName);
        Assert.Equal("Nunez", sub.PlayerOutName);
    }

    [Fact]
    public async Task GetMatchLineupsAsync_MapsFormationsAndPlayers()
    {
        var client = CreateClient(JsonResponse("""
            {"confirmed":true,
             "home":{"formation":"4-3-3","players":[
               {"player":{"name":"Raya"},"jerseyNumber":"22","substitute":false,"position":"G"}]},
             "away":{"formation":"4-2-3-1","players":[
               {"player":{"name":"Alisson"},"jerseyNumber":"1","substitute":false,"position":"G"}]}}
            """));

        var lineups = await client.GetMatchLineupsAsync(14025269);

        Assert.NotNull(lineups);
        Assert.True(lineups!.Confirmed);
        Assert.Equal("4-3-3", lineups.Home!.Formation);
        var player = Assert.Single(lineups.Home.Players);
        Assert.Equal("Raya", player.Name);
        Assert.Equal(22, player.Number);
        Assert.Equal("4-2-3-1", lineups.Away!.Formation);
    }

    [Fact]
    public async Task GetMatchLineupsAsync_ReturnsNull_OnNoContent()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent));

        var lineups = await client.GetMatchLineupsAsync(14025269);

        Assert.Null(lineups);
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

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchDetailProviderTests -p:UseAppHost=false`
Expected: FAIL — methods don't exist (compile error).

- [ ] **Step 5: Implement in FootApiClient** — add these methods after `GetMatchLogoAsync`/near the other gets, and the private parse records next to the existing ones, in `FootApiClient.cs`:

```csharp
    public async Task<IReadOnlyCollection<FootballStatItem>> GetMatchStatisticsAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetMatchResourceAsync<FootApiStatisticsResponse>(
            eventId, "statistics", cancellationToken);

        var allPeriod = payload?.Statistics?.FirstOrDefault(period =>
            string.Equals(period.Period, "ALL", StringComparison.OrdinalIgnoreCase));

        return allPeriod?.Groups?
            .SelectMany(group => group.StatisticsItems ?? [])
            .Select(item => new FootballStatItem(
                item.Name ?? string.Empty,
                item.Home ?? string.Empty,
                item.Away ?? string.Empty))
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyCollection<FootballIncident>> GetMatchIncidentsAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetMatchResourceAsync<FootApiIncidentsResponse>(
            eventId, "incidents", cancellationToken);

        return payload?.Incidents?
            .Where(incident => !string.IsNullOrWhiteSpace(incident.IncidentType))
            .Select(incident => new FootballIncident(
                incident.IncidentType!,
                incident.Time,
                incident.IsHome ?? false,
                incident.Player?.Name ?? string.Empty,
                incident.PlayerIn?.Name ?? string.Empty,
                incident.PlayerOut?.Name ?? string.Empty,
                incident.Text ?? string.Empty))
            .ToArray() ?? [];
    }

    public async Task<FootballLineups?> GetMatchLineupsAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetMatchResourceAsync<FootApiLineupsResponse>(
            eventId, "lineups", cancellationToken);

        if (payload is null)
        {
            return null;
        }

        return new FootballLineups(
            payload.Confirmed ?? false,
            MapLineupTeam(payload.Home),
            MapLineupTeam(payload.Away));
    }

    private static FootballLineupTeam? MapLineupTeam(FootApiLineupTeam? team)
    {
        if (team is null)
        {
            return null;
        }

        var players = (team.Players ?? [])
            .Select(entry => new FootballLineupPlayer(
                entry.Player?.Name ?? string.Empty,
                int.TryParse(entry.JerseyNumber, out var number) ? number : 0,
                entry.Substitute ?? false,
                entry.Position ?? string.Empty))
            .ToArray();

        return new FootballLineupTeam(team.Formation ?? string.Empty, players);
    }

    private async Task<TPayload?> GetMatchResourceAsync<TPayload>(
        int eventId,
        string resource,
        CancellationToken cancellationToken)
        where TPayload : class
    {
        using var request = CreateRequest($"/api/match/{eventId}/{resource}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TPayload>(responseStream, JsonOptions, cancellationToken);
    }

    private sealed record FootApiStatisticsResponse(IReadOnlyCollection<FootApiStatPeriod>? Statistics);
    private sealed record FootApiStatPeriod(string? Period, IReadOnlyCollection<FootApiStatGroup>? Groups);
    private sealed record FootApiStatGroup(string? GroupName, IReadOnlyCollection<FootApiStatEntry>? StatisticsItems);
    private sealed record FootApiStatEntry(string? Name, string? Home, string? Away);

    private sealed record FootApiIncidentsResponse(IReadOnlyCollection<FootApiIncident>? Incidents);
    private sealed record FootApiIncident(
        string? IncidentType,
        int Time,
        bool? IsHome,
        FootApiNamed? Player,
        FootApiNamed? PlayerIn,
        FootApiNamed? PlayerOut,
        string? Text);
    private sealed record FootApiNamed(string? Name);

    private sealed record FootApiLineupsResponse(bool? Confirmed, FootApiLineupTeam? Home, FootApiLineupTeam? Away);
    private sealed record FootApiLineupTeam(string? Formation, IReadOnlyCollection<FootApiLineupEntry>? Players);
    private sealed record FootApiLineupEntry(FootApiNamed? Player, string? JerseyNumber, bool? Substitute, string? Position);
```

- [ ] **Step 6: Add the three methods to all five test fakes** — in each `Fake...Provider : IFootballProvider` (in `StandingsServiceTests.cs`, `TeamSyncServiceTests.cs`, `TeamLogoSyncServiceTests.cs`, `IntegrationEndpointsTests.cs`, `MatchSyncServiceTests.cs`) add:

```csharp
        public Task<IReadOnlyCollection<FootballStatItem>> GetMatchStatisticsAsync(
            int eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<FootballStatItem>>([]);

        public Task<IReadOnlyCollection<FootballIncident>> GetMatchIncidentsAsync(
            int eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<FootballIncident>>([]);

        public Task<FootballLineups?> GetMatchLineupsAsync(
            int eventId, CancellationToken cancellationToken = default)
            => Task.FromResult<FootballLineups?>(null);
```

- [ ] **Step 7: Run the test + confirm compile**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchDetailProviderTests -p:UseAppHost=false`
Expected: PASS (4 tests).

- [ ] **Step 8: Commit (production only)**

```bash
git add backend/PLeagueHub.Api/Services/Football/FootballMatchDetail.cs backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs backend/PLeagueHub.Api/Services/Football/FootApiClient.cs
git commit -m "Add FootApi match statistics, incidents and lineups"
```

---

## Task 2: DTOs + MatchDetailService

**Files:**
- Create: `backend/PLeagueHub.Api/Responses/MatchDetailResponse.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/MatchDetailUnavailableException.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/MatchDetailService.cs`
- Test: `backend/PLeagueHub.Api.Tests/MatchDetailServiceTests.cs`

- [ ] **Step 1: Add DTOs and exception**

`MatchDetailResponse.cs`:
```csharp
namespace PLeagueHub.Api.Responses;

public sealed record MatchTeamDto(string Naziv, string Skracenica, string LogoUrl);

public sealed record MatchHeaderDto(
    MatchTeamDto Domacin,
    MatchTeamDto Gost,
    int? GolDomacin,
    int? GolGost,
    int Kolo,
    string Sezona,
    string Status,
    DateTime Datum);

public sealed record StatItemDto(string Naziv, string Domacin, string Gost);

public sealed record IncidentDto(string Tip, int Minut, bool Domacin, string Tekst);

public sealed record LineupPlayerDto(string Ime, int Broj, bool Zamena, string Pozicija);

public sealed record LineupTeamDto(string Formacija, IReadOnlyCollection<LineupPlayerDto> Igraci);

public sealed record LineupsDto(bool Potvrdjeno, LineupTeamDto Domacin, LineupTeamDto Gost);

public sealed record MatchDetailResponse(
    MatchHeaderDto Header,
    IReadOnlyCollection<StatItemDto> Statistics,
    IReadOnlyCollection<IncidentDto> Incidents,
    LineupsDto? Lineups);
```

`MatchDetailUnavailableException.cs`:
```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed class MatchDetailUnavailableException : Exception
{
    public MatchDetailUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [ ] **Step 2: Write the failing test** — create `MatchDetailServiceTests.cs`:

```csharp
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class MatchDetailServiceTests
{
    private static readonly Team Home = new() { Id = "h", Naziv = "Arsenal", Skracenica = "ARS", LogoUrl = "/a.png" };
    private static readonly Team Away = new() { Id = "a", Naziv = "Liverpool", Skracenica = "LIV", LogoUrl = "/l.png" };

    private static Match FinishedMatch() => new()
    {
        Id = "m1", DomacinId = "h", GostId = "a", ProviderId = 999,
        Kolo = 5, Sezona = "2024/25", Status = "zavrsena", GolDomacin = 2, GolGost = 1,
        Datum = new DateTime(2024, 9, 1, 14, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenMatchMissing()
    {
        var service = CreateService(new FakeMatchRepo(null), new FakeProvider());
        Assert.Null(await service.GetAsync("nope", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_BuildsHeaderAndMapsDetail()
    {
        var provider = new FakeProvider
        {
            Stats = [new FootballStatItem("Ball possession", "55%", "45%")],
            Incidents = [new FootballIncident("goal", 23, true, "Saka", "", "", "")],
            Lineups = new FootballLineups(true,
                new FootballLineupTeam("4-3-3", [new FootballLineupPlayer("Raya", 22, false, "G")]),
                new FootballLineupTeam("4-2-3-1", [new FootballLineupPlayer("Alisson", 1, false, "G")]))
        };
        var service = CreateService(new FakeMatchRepo(FinishedMatch()), provider);

        var result = await service.GetAsync("m1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Arsenal", result!.Header.Domacin.Naziv);
        Assert.Equal(2, result.Header.GolDomacin);
        Assert.Equal("Ball possession", Assert.Single(result.Statistics).Naziv);
        Assert.Equal("goal", Assert.Single(result.Incidents).Tip);
        Assert.Equal("4-3-3", result.Lineups!.Domacin.Formacija);
    }

    [Fact]
    public async Task GetAsync_ReturnsHeaderOnly_WhenNoProviderId()
    {
        var match = FinishedMatch();
        match.ProviderId = null;
        var provider = new FakeProvider { ThrowOnCall = true };
        var service = CreateService(new FakeMatchRepo(match), provider);

        var result = await service.GetAsync("m1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result!.Statistics);
        Assert.Null(result.Lineups);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task GetAsync_CachesFootApiResultPerProviderId()
    {
        var provider = new FakeProvider();
        var service = CreateService(new FakeMatchRepo(FinishedMatch()), provider);

        await service.GetAsync("m1", CancellationToken.None);
        await service.GetAsync("m1", CancellationToken.None);

        Assert.Equal(1, provider.CallCount); // statistics fetched once
    }

    [Fact]
    public async Task GetAsync_WrapsProviderFailure()
    {
        var provider = new FakeProvider { ThrowOnCall = true };
        var service = CreateService(new FakeMatchRepo(FinishedMatch()), provider);

        await Assert.ThrowsAsync<MatchDetailUnavailableException>(
            () => service.GetAsync("m1", CancellationToken.None));
    }

    private static MatchDetailService CreateService(FakeMatchRepo matches, FakeProvider provider)
        => new(matches, new FakeTeamRepo(), provider, new NoopPacer(), new MemoryCache(new MemoryCacheOptions()));

    private sealed class FakeProvider : IFootballProvider
    {
        public IReadOnlyCollection<FootballStatItem> Stats { get; set; } = [];
        public IReadOnlyCollection<FootballIncident> Incidents { get; set; } = [];
        public FootballLineups? Lineups { get; set; }
        public bool ThrowOnCall { get; set; }
        public int CallCount { get; private set; }

        public Task<IReadOnlyCollection<FootballStatItem>> GetMatchStatisticsAsync(int eventId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (ThrowOnCall) throw new HttpRequestException("boom");
            return Task.FromResult(Stats);
        }
        public Task<IReadOnlyCollection<FootballIncident>> GetMatchIncidentsAsync(int eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(Incidents);
        public Task<FootballLineups?> GetMatchLineupsAsync(int eventId, CancellationToken cancellationToken = default)
            => Task.FromResult(Lineups);

        public Task<System.Text.Json.JsonDocument> SearchAsync(string term, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(int t, int s, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FootballTeamLogo> GetTeamLogoAsync(int p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(int t, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<FootballEvent>> GetSeasonEventsAsync(int t, int s, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeMatchRepo(Match? match) : IRepository<Match>
    {
        public Task<Match?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(match is not null && match.Id == id ? match : null);
        public Task<IReadOnlyCollection<Match>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Match?> FindOneAsync(Expression<Func<Match, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Expression<Func<Match, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Match> CreateAsync(Match e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(string id, Match e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTeamRepo : IRepository<Team>
    {
        public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<Team?>(id == "h" ? Home : id == "a" ? Away : null);
        public Task<IReadOnlyCollection<Team>> GetAllAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Team?> FindOneAsync(Expression<Func<Team, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Expression<Func<Team, bool>> p, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Team> CreateAsync(Team e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(string id, Team e, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoopPacer : IProviderRequestPacer
    {
        public Task WaitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchDetailServiceTests -p:UseAppHost=false`
Expected: FAIL — `MatchDetailService` does not exist.

- [ ] **Step 4: Implement the service** — create `MatchDetailService.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IMatchDetailService
{
    Task<MatchDetailResponse?> GetAsync(string matchId, CancellationToken cancellationToken);
}

public sealed class MatchDetailService : IMatchDetailService
{
    private static readonly TimeSpan FinishedTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan LiveTtl = TimeSpan.FromMinutes(2);

    private readonly IRepository<Match> _matches;
    private readonly IRepository<Team> _teams;
    private readonly IFootballProvider _provider;
    private readonly IProviderRequestPacer _pacer;
    private readonly IMemoryCache _cache;

    public MatchDetailService(
        IRepository<Match> matches,
        IRepository<Team> teams,
        IFootballProvider provider,
        IProviderRequestPacer pacer,
        IMemoryCache cache)
    {
        _matches = matches;
        _teams = teams;
        _provider = provider;
        _pacer = pacer;
        _cache = cache;
    }

    public async Task<MatchDetailResponse?> GetAsync(string matchId, CancellationToken cancellationToken)
    {
        var match = await _matches.GetByIdAsync(matchId, cancellationToken);

        if (match is null)
        {
            return null;
        }

        var header = await BuildHeaderAsync(match, cancellationToken);

        if (match.ProviderId is not int providerId)
        {
            return new MatchDetailResponse(header, [], [], null);
        }

        var detail = await GetCachedDetailAsync(providerId, match.Status, cancellationToken);
        return new MatchDetailResponse(header, detail.Statistics, detail.Incidents, detail.Lineups);
    }

    private async Task<CachedDetail> GetCachedDetailAsync(int providerId, string status, CancellationToken cancellationToken)
    {
        var cacheKey = $"match-detail:{providerId}";

        if (_cache.TryGetValue(cacheKey, out CachedDetail? cached) && cached is not null)
        {
            return cached;
        }

        IReadOnlyCollection<FootballStatItem> stats;
        IReadOnlyCollection<FootballIncident> incidents;
        FootballLineups? lineups;

        try
        {
            await _pacer.WaitAsync(cancellationToken);
            stats = await _provider.GetMatchStatisticsAsync(providerId, cancellationToken);
            await _pacer.WaitAsync(cancellationToken);
            incidents = await _provider.GetMatchIncidentsAsync(providerId, cancellationToken);
            await _pacer.WaitAsync(cancellationToken);
            lineups = await _provider.GetMatchLineupsAsync(providerId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new MatchDetailUnavailableException("FootAPI match detail could not be loaded.", exception);
        }

        cached = new CachedDetail(MapStats(stats), MapIncidents(incidents), MapLineups(lineups));
        _cache.Set(cacheKey, cached, status == "zavrsena" ? FinishedTtl : LiveTtl);
        return cached;
    }

    private async Task<MatchHeaderDto> BuildHeaderAsync(Match match, CancellationToken cancellationToken)
    {
        var home = await _teams.GetByIdAsync(match.DomacinId, cancellationToken);
        var away = await _teams.GetByIdAsync(match.GostId, cancellationToken);

        return new MatchHeaderDto(
            ToTeamDto(home),
            ToTeamDto(away),
            match.GolDomacin,
            match.GolGost,
            match.Kolo,
            match.Sezona,
            match.Status,
            match.Datum);
    }

    private static MatchTeamDto ToTeamDto(Team? team)
        => new(team?.Naziv ?? "Nepoznat tim", team?.Skracenica ?? string.Empty, team?.LogoUrl ?? string.Empty);

    private static IReadOnlyCollection<StatItemDto> MapStats(IReadOnlyCollection<FootballStatItem> stats)
        => stats.Select(item => new StatItemDto(item.Name, item.Home, item.Away)).ToArray();

    private static IReadOnlyCollection<IncidentDto> MapIncidents(IReadOnlyCollection<FootballIncident> incidents)
        => incidents
            .OrderBy(incident => incident.Minute)
            .Select(incident => new IncidentDto(
                incident.Type,
                incident.Minute,
                incident.IsHome,
                BuildIncidentText(incident)))
            .ToArray();

    private static string BuildIncidentText(FootballIncident incident)
        => incident.Type switch
        {
            "substitution" => $"{incident.PlayerInName} ↑ / {incident.PlayerOutName} ↓",
            _ => string.IsNullOrWhiteSpace(incident.PlayerName) ? incident.Detail : incident.PlayerName
        };

    private static LineupsDto? MapLineups(FootballLineups? lineups)
    {
        if (lineups?.Home is null || lineups.Away is null)
        {
            return null;
        }

        return new LineupsDto(lineups.Confirmed, MapLineupTeam(lineups.Home), MapLineupTeam(lineups.Away));
    }

    private static LineupTeamDto MapLineupTeam(FootballLineupTeam team)
        => new(
            team.Formation,
            team.Players
                .Select(player => new LineupPlayerDto(player.Name, player.Number, player.IsSubstitute, player.Position))
                .ToArray());

    private sealed record CachedDetail(
        IReadOnlyCollection<StatItemDto> Statistics,
        IReadOnlyCollection<IncidentDto> Incidents,
        LineupsDto? Lineups);
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchDetailServiceTests -p:UseAppHost=false`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Responses/MatchDetailResponse.cs backend/PLeagueHub.Api/Services/Football/MatchDetailUnavailableException.cs backend/PLeagueHub.Api/Services/Football/MatchDetailService.cs
git commit -m "Add cached match detail service"
```

---

## Task 3: Controller + DI

**Files:**
- Create: `backend/PLeagueHub.Api/Controllers/MatchDetailController.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test: `backend/PLeagueHub.Api.Tests/MatchDetailControllerTests.cs`

- [ ] **Step 1: Write the failing test** — create `MatchDetailControllerTests.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Controllers;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class MatchDetailControllerTests
{
    private static MatchDetailResponse Sample() => new(
        new MatchHeaderDto(new MatchTeamDto("A", "A", ""), new MatchTeamDto("B", "B", ""),
            1, 0, 1, "2024/25", "zavrsena", DateTime.UtcNow),
        [], [], null);

    [Fact]
    public async Task GetDetail_ReturnsOk_WhenFound()
    {
        var controller = new MatchDetailController(new FakeService { Result = Sample() });
        var result = await controller.GetDetailAsync("m1", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<MatchDetailResponse>(ok.Value);
    }

    [Fact]
    public async Task GetDetail_Returns404_WhenMissing()
    {
        var controller = new MatchDetailController(new FakeService { Result = null });
        var result = await controller.GetDetailAsync("m1", CancellationToken.None);
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetDetail_Returns502_OnProviderFailure()
    {
        var controller = new MatchDetailController(new FakeService { Throw = true });
        var result = await controller.GetDetailAsync("m1", CancellationToken.None);
        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
    }

    private sealed class FakeService : IMatchDetailService
    {
        public MatchDetailResponse? Result { get; set; }
        public bool Throw { get; set; }

        public Task<MatchDetailResponse?> GetAsync(string matchId, CancellationToken cancellationToken)
        {
            if (Throw)
            {
                throw new MatchDetailUnavailableException("down", new HttpRequestException());
            }

            return Task.FromResult(Result);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchDetailControllerTests -p:UseAppHost=false`
Expected: FAIL — `MatchDetailController` does not exist.

- [ ] **Step 3: Implement the controller** — create `MatchDetailController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchDetailController : ControllerBase
{
    private readonly IMatchDetailService _matchDetailService;

    public MatchDetailController(IMatchDetailService matchDetailService)
    {
        _matchDetailService = matchDetailService;
    }

    [HttpGet("{matchId}/detail")]
    [ProducesResponseType(typeof(MatchDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<MatchDetailResponse>> GetDetailAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _matchDetailService.GetAsync(matchId, cancellationToken);

            if (detail is null)
            {
                return NotFound();
            }

            return Ok(detail);
        }
        catch (MatchDetailUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
    }
}
```

- [ ] **Step 4: Register the service** — in `Program.cs`, next to `builder.Services.AddScoped<IStandingsService, StandingsService>();` add:

```csharp
builder.Services.AddScoped<IMatchDetailService, MatchDetailService>();
```

- [ ] **Step 5: Run tests + full build**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchDetailControllerTests -p:UseAppHost=false`
Expected: PASS (3 tests).
Run: `dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj -p:UseAppHost=false`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Controllers/MatchDetailController.cs backend/PLeagueHub.Api/Program.cs
git commit -m "Expose match detail endpoint"
```

---

## Task 4: Frontend types + matchDetailApi

**Files:**
- Modify: `frontend/src/types/api.ts`
- Create: `frontend/src/services/matchDetailApi.ts`
- Test: `frontend/src/services/matchDetailApi.test.ts`

- [ ] **Step 1: Add types** — in `types/api.ts`, add `providerId` to `Match` (after `zavrsenaAt`):

```ts
  zavrsenaAt?: string | null;
  providerId?: number | null;
```

and append the detail types:

```ts
export interface MatchTeamInfo {
  naziv: string;
  skracenica: string;
  logoUrl: string;
}

export interface MatchHeader {
  domacin: MatchTeamInfo;
  gost: MatchTeamInfo;
  golDomacin: number | null;
  golGost: number | null;
  kolo: number;
  sezona: string;
  status: string;
  datum: string;
}

export interface StatItem {
  naziv: string;
  domacin: string;
  gost: string;
}

export interface Incident {
  tip: string;
  minut: number;
  domacin: boolean;
  tekst: string;
}

export interface LineupPlayer {
  ime: string;
  broj: number;
  zamena: boolean;
  pozicija: string;
}

export interface LineupTeam {
  formacija: string;
  igraci: LineupPlayer[];
}

export interface Lineups {
  potvrdjeno: boolean;
  domacin: LineupTeam;
  gost: LineupTeam;
}

export interface MatchDetail {
  header: MatchHeader;
  statistics: StatItem[];
  incidents: Incident[];
  lineups: Lineups | null;
}
```

- [ ] **Step 2: Write the failing test** — create `services/matchDetailApi.test.ts`:

```ts
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { get } = vi.hoisted(() => ({ get: vi.fn().mockResolvedValue({ data: {} }) }));
vi.mock('./api', () => ({ api: { get } }));

import { matchDetailApi } from './matchDetailApi';

describe('matchDetailApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('requests the detail of a match by id', () => {
    matchDetailApi.get('m1');
    expect(get).toHaveBeenCalledWith('/api/matches/m1/detail');
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd frontend && npm test -- matchDetailApi`
Expected: FAIL — cannot find `./matchDetailApi`.

- [ ] **Step 4: Implement** — create `services/matchDetailApi.ts`:

```ts
import type { MatchDetail } from '../types/api';
import { api } from './api';

export const matchDetailApi = {
  async get(matchId: string) {
    const response = await api.get<MatchDetail>(`/api/matches/${matchId}/detail`);
    return response.data;
  }
};
```

- [ ] **Step 5: Run the test**

Run: `cd frontend && npm test -- matchDetailApi`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/src/types/api.ts frontend/src/services/matchDetailApi.ts
git commit -m "Add match detail API client and types"
```

---

## Task 5: MatchDetail page

**Files:**
- Create: `frontend/src/pages/MatchDetail.tsx`
- Test: `frontend/src/pages/MatchDetail.test.tsx`

- [ ] **Step 1: Write the failing test** — create `pages/MatchDetail.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { get } = vi.hoisted(() => ({ get: vi.fn() }));
vi.mock('../services/matchDetailApi', () => ({ matchDetailApi: { get } }));

import { MatchDetailPage } from './MatchDetail';

const detail = {
  header: {
    domacin: { naziv: 'Arsenal', skracenica: 'ARS', logoUrl: '' },
    gost: { naziv: 'Liverpool', skracenica: 'LIV', logoUrl: '' },
    golDomacin: 2, golGost: 1, kolo: 5, sezona: '2024/25', status: 'zavrsena',
    datum: '2024-09-01T14:00:00Z'
  },
  statistics: [{ naziv: 'Posed lopte', domacin: '55%', gost: '45%' }],
  incidents: [{ tip: 'goal', minut: 23, domacin: true, tekst: 'Saka' }],
  lineups: {
    potvrdjeno: true,
    domacin: { formacija: '4-3-3', igraci: [{ ime: 'Raya', broj: 22, zamena: false, pozicija: 'G' }] },
    gost: { formacija: '4-2-3-1', igraci: [{ ime: 'Alisson', broj: 1, zamena: false, pozicija: 'G' }] }
  }
};

function renderAt(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/mec/${id}`]}>
      <Routes>
        <Route path="/mec/:id" element={<MatchDetailPage />} />
      </Routes>
    </MemoryRouter>
  );
}

describe('MatchDetailPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders header, statistics, incidents and lineups', async () => {
    get.mockResolvedValue(detail);
    renderAt('m1');

    expect(await screen.findByText('Arsenal')).toBeInTheDocument();
    expect(screen.getByText('Liverpool')).toBeInTheDocument();
    expect(screen.getByText('Posed lopte')).toBeInTheDocument();
    expect(screen.getByText('Saka')).toBeInTheDocument();
    expect(screen.getByText('Raya')).toBeInTheDocument();
    expect(get).toHaveBeenCalledWith('m1');
  });

  it('shows the after-match notice when there is no statistics data', async () => {
    get.mockResolvedValue({ ...detail, statistics: [], incidents: [], lineups: null });
    renderAt('m2');

    expect(await screen.findByText('Arsenal')).toBeInTheDocument();
    expect(screen.getByText(/statistika dostupna nakon meca/i)).toBeInTheDocument();
  });

  it('shows an error state on failure', async () => {
    get.mockRejectedValue(new Error('boom'));
    renderAt('m3');

    expect(await screen.findByText(/nije moguce ucitati detalje/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- MatchDetail`
Expected: FAIL — cannot find `./MatchDetail`.

- [ ] **Step 3: Implement the page** — create `pages/MatchDetail.tsx`:

```tsx
import { ArrowLeft, Goal, RectangleVertical } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { matchDetailApi } from '../services/matchDetailApi';
import type { MatchDetail } from '../types/api';
import { TeamLogo } from '../components/TeamLogo';

function statPercent(value: string): number {
  const parsed = Number.parseFloat(value.replace('%', ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

export function MatchDetailPage() {
  const { id } = useParams();
  const [detail, setDetail] = useState<MatchDetail | null>(null);
  const [status, setStatus] = useState<'loading' | 'error' | 'ready'>('loading');

  useEffect(() => {
    if (!id) {
      return;
    }

    setStatus('loading');
    matchDetailApi.get(id)
      .then((data) => {
        setDetail(data);
        setStatus('ready');
      })
      .catch(() => setStatus('error'));
  }, [id]);

  if (status === 'loading') {
    return <p className="px-4 py-10 text-center text-sm text-slate-400">Ucitavanje detalja...</p>;
  }

  if (status === 'error' || !detail) {
    return <p className="px-4 py-10 text-center text-sm text-red-500">Trenutno nije moguce ucitati detalje meca.</p>;
  }

  const { header, statistics, incidents, lineups } = detail;
  const played = header.golDomacin !== null && header.golGost !== null;
  const empty = statistics.length === 0 && incidents.length === 0 && !lineups;

  return (
    <div className="space-y-4">
      <Link to="/results" className="inline-flex items-center gap-1 text-xs font-semibold text-slate-500 hover:text-brand">
        <ArrowLeft size={14} /> Rezultati
      </Link>

      <section className="rounded-lg border border-slate-200 bg-white p-5 shadow-sm">
        <p className="text-center text-[10px] font-bold uppercase text-slate-400">
          {header.sezona} · {header.kolo}. kolo
        </p>
        <div className="mt-3 grid grid-cols-[1fr_auto_1fr] items-center gap-3">
          <div className="flex flex-col items-center gap-2 text-center">
            <TeamLogo className="size-12" logoUrl={header.domacin.logoUrl} name={header.domacin.naziv} />
            <span className="text-sm font-bold">{header.domacin.naziv}</span>
          </div>
          <div className="text-center">
            {played ? (
              <p className="text-3xl font-black">{header.golDomacin} : {header.golGost}</p>
            ) : (
              <p className="text-sm font-bold text-brand">
                {new Date(header.datum).toLocaleDateString('sr-RS', { day: '2-digit', month: '2-digit' })}
                <br />
                {new Date(header.datum).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
              </p>
            )}
            <p className="mt-1 text-[10px] font-semibold uppercase text-slate-400">{header.status}</p>
          </div>
          <div className="flex flex-col items-center gap-2 text-center">
            <TeamLogo className="size-12" logoUrl={header.gost.logoUrl} name={header.gost.naziv} />
            <span className="text-sm font-bold">{header.gost.naziv}</span>
          </div>
        </div>
      </section>

      {empty && (
        <section className="rounded-lg border border-slate-200 bg-white p-6 text-center text-sm text-slate-400 shadow-sm">
          Statistika dostupna nakon meca.
        </section>
      )}

      {incidents.length > 0 && (
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
          <h2 className="bg-ink px-4 py-2 text-[11px] font-bold uppercase text-slate-300">Tok meca</h2>
          <ul className="divide-y divide-slate-100">
            {incidents.map((incident, index) => (
              <li key={`${incident.minut}-${index}`} className={`flex items-center gap-2 px-4 py-2 text-sm ${incident.domacin ? '' : 'flex-row-reverse text-right'}`}>
                <span className="w-8 text-xs font-bold text-slate-400">{incident.minut}'</span>
                {incident.tip === 'goal' ? <Goal size={15} className="text-emerald-600" /> : <RectangleVertical size={15} className="text-amber-500" />}
                <span className="font-semibold">{incident.tekst}</span>
              </li>
            ))}
          </ul>
        </section>
      )}

      {statistics.length > 0 && (
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="mb-3 text-[11px] font-bold uppercase text-slate-400">Statistika</h2>
          <div className="space-y-3">
            {statistics.map((stat) => {
              const home = statPercent(stat.domacin);
              const total = home + statPercent(stat.gost);
              const homePct = total > 0 ? (home / total) * 100 : 50;
              return (
                <div key={stat.naziv}>
                  <div className="flex justify-between text-xs font-semibold">
                    <span>{stat.domacin}</span>
                    <span className="text-slate-500">{stat.naziv}</span>
                    <span>{stat.gost}</span>
                  </div>
                  <div className="mt-1 flex h-1.5 overflow-hidden rounded bg-slate-200">
                    <div className="bg-brand" style={{ width: `${homePct}%` }} />
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      )}

      {lineups && (
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <h2 className="mb-3 text-[11px] font-bold uppercase text-slate-400">Postave</h2>
          <div className="grid grid-cols-2 gap-4">
            {[lineups.domacin, lineups.gost].map((team, side) => (
              <div key={side}>
                <p className="mb-2 text-xs font-bold text-brand">{team.formacija}</p>
                <ul className="space-y-1">
                  {team.igraci.filter((player) => !player.zamena).map((player) => (
                    <li key={player.ime} className="flex items-center gap-2 text-xs">
                      <span className="w-5 text-right font-bold text-slate-400">{player.broj}</span>
                      <span className="font-semibold">{player.ime}</span>
                    </li>
                  ))}
                </ul>
              </div>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run the tests**

Run: `cd frontend && npm test -- MatchDetail`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/MatchDetail.tsx
git commit -m "Add match detail page"
```

---

## Task 6: Clickable rows + route

**Files:**
- Modify: `frontend/src/components/MatchRow.tsx`
- Modify: `frontend/src/App.tsx`
- Test: `frontend/src/components/MatchRow.test.tsx`

- [ ] **Step 1: Write the failing test** — create `components/MatchRow.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { MatchRow } from './MatchRow';

const teams = new Map([
  ['h', { id: 'h', naziv: 'Arsenal', skracenica: 'ARS', logoUrl: '', bodovi: 0, pozicija: 0, stadion: '', osnovan: 0 }],
  ['a', { id: 'a', naziv: 'Chelsea', skracenica: 'CHE', logoUrl: '', bodovi: 0, pozicija: 0, stadion: '', osnovan: 0 }]
]) as never;

const match = {
  id: 'm1', domacinId: 'h', gostId: 'a', datum: '2024-09-01T14:00:00Z', kolo: 1,
  sezona: '2024/25', golDomacin: 2, golGost: 1, status: 'zavrsena'
} as never;

describe('MatchRow', () => {
  it('links to the match detail page', () => {
    render(<MemoryRouter><MatchRow match={match} teams={teams} /></MemoryRouter>);
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/mec/m1');
    expect(screen.getByText('Arsenal')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- MatchRow`
Expected: FAIL — current `MatchRow` renders a `div`, no link.

- [ ] **Step 3: Make the row a link** — in `MatchRow.tsx`, change the import line and wrap the row. Replace the top import:

```tsx
import { Link } from 'react-router-dom';
import type { Match, Team } from '../types/api';
import { RelativeTime } from './RelativeTime';
import { TeamIdentity } from './TeamIdentity';
```

and change the outer element from `<div ...>` to a `Link` (keep all inner content and classes identical, add hover):

```tsx
    <Link
      to={`/mec/${match.id}`}
      className="grid grid-cols-[56px_minmax(0,1fr)_44px] items-center gap-3 border-b border-slate-100 px-3 py-3 last:border-0 hover:bg-slate-50 sm:grid-cols-[72px_minmax(0,1fr)_64px]"
    >
```

and change the matching closing `</div>` of that outer element to `</Link>`.

- [ ] **Step 4: Add the route** — in `App.tsx`, add the import after the `Stats`/`TablePage` imports:

```tsx
import { MatchDetailPage } from './pages/MatchDetail';
```

and add the route after the `results` route:

```tsx
        <Route path="results" element={<Results />} />
        <Route path="mec/:id" element={<MatchDetailPage />} />
```

- [ ] **Step 5: Run tests + full build + suite**

Run: `cd frontend && npm test -- MatchRow`
Expected: PASS.
Run: `cd frontend && npm run build`
Expected: tsc + vite succeed.
Run: `cd frontend && npm test`
Expected: all suites pass.

- [ ] **Step 6: Commit**

```bash
git add frontend/src/components/MatchRow.tsx frontend/src/App.tsx
git commit -m "Make match rows open the detail page"
```

---

## Final Verification (verification-before-completion)

- [ ] Backend: `dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj -p:UseAppHost=false` → 0 errors; `dotnet test ... --filter "MatchDetailProviderTests|MatchDetailServiceTests|MatchDetailControllerTests" -p:UseAppHost=false` → green.
- [ ] Frontend: `cd frontend && npm test` → all green; `npm run build` → succeeds.
- [ ] **Live (after FootApi daily quota resets):** start API + frontend, open Results, click a finished match → header + statistics + timeline + lineups render; click a scheduled match → header + "Statistika dostupna nakon meca". Reload → served from cache (no FootApi call).
- [ ] **Assumption check:** confirm the live `/api/match/{id}/statistics|incidents|lineups` field names (`statistics[].period`/`groups`/`statisticsItems`, `incidents[].incidentType`/`time`/`isHome`/`player`/`playerIn`/`playerOut`, `lineups.home.formation`/`players[].player.name`/`jerseyNumber`/`substitute`). Adjust the private parse records in `FootApiClient` if different.

## Out of Scope (v1)
Head-to-head, odds, player ratings/heatmaps, live auto-refresh, pre-ingesting details.
