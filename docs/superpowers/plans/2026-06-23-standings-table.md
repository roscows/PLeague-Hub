# Standings Table ("Tabela") Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "Tabela" nav entry that opens a full Premier League standings table with a season selector for previous seasons, sourced live from FootApi with server-side caching.

**Architecture:** Backend extends the FootApi standings parse to full classic columns, adds a seasons-list provider call, and exposes a public, cached `StandingsService` behind a `StandingsController`. Frontend adds a `standingsApi`, a `TablePage`, a route, and a nav item. No new MongoDB storage.

**Tech Stack:** .NET 10 (ASP.NET Core controllers, `IMemoryCache`, xUnit), React 19 + TypeScript + Tailwind + Vite, axios, vitest + Testing Library, lucide-react.

**Reference spec:** `docs/superpowers/specs/2026-06-23-standings-table-design.md`

**Backend test command:** `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter <Name>` (FootApi/service/controller unit tests need no MongoDB).
**Frontend test command:** `npm test --prefix frontend` (or `cd frontend && npm test`).

---

## File Structure

**Backend (create):**
- `Services/Football/FootballSeason.cs` — provider season record.
- `Services/Football/StandingsService.cs` + `IStandingsService` — orchestration, cache, logo join, ordering, DTO mapping.
- `Services/Football/StandingsUnavailableException.cs` — provider-failure signal → 502.
- `Responses/SeasonResponse.cs`, `Responses/StandingRowResponse.cs` — API DTOs.
- `Controllers/StandingsController.cs` — public read endpoints.
- `PLeagueHub.Api.Tests/StandingsServiceTests.cs`, `PLeagueHub.Api.Tests/StandingsControllerTests.cs`.

**Backend (modify):**
- `Services/Football/FootballTeamStanding.cs` — add P/W/D/L/GF/GA + computed GD.
- `Services/Football/IFootballProvider.cs` — add `GetSeasonsAsync`.
- `Services/Football/FootApiClient.cs` — parse full row fields + implement `GetSeasonsAsync`.
- `Services/Football/TeamSyncService.cs:91,104-108` — update the single `FootballTeamStanding` construction site.
- `Program.cs` — `AddMemoryCache()` + register `IStandingsService`.
- `PLeagueHub.Api.Tests/FootApiClientTests.cs` — add full-columns + seasons parse tests.

**Frontend (create):**
- `services/standingsApi.ts`, `services/standingsApi.test.ts`.
- `pages/Table.tsx`, `pages/Table.test.tsx`.

**Frontend (modify):**
- `types/api.ts` — `Season`, `StandingRow`.
- `App.tsx` — `/tabela` route.
- `components/Layout.tsx:1-12,22-28` — import `ListOrdered`, add nav item between Statistike and Vesti.

---

## Task 1: Extend standings model + FootApi full-column parse

**Files:**
- Modify: `backend/PLeagueHub.Api/Services/Football/FootballTeamStanding.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/FootApiClient.cs:34-60,135-138`
- Modify: `backend/PLeagueHub.Api/Services/Football/TeamSyncService.cs:91,104-108`
- Test: `backend/PLeagueHub.Api.Tests/FootApiClientTests.cs`

- [ ] **Step 1: Write the failing test** — append to `FootApiClientTests.cs` (inside the class):

```csharp
    [Fact]
    public async Task GetTeamStandingsAsync_MapsFullClassicColumns()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {"standings":[{"rows":[
              {"team":{"id":42,"name":"Arsenal","nameCode":"ARS"},
               "position":1,"matches":10,"wins":8,"draws":1,"losses":1,
               "scoresFor":24,"scoresAgainst":7,"points":25}
            ]}]}
            """));
        var client = CreateClient(handler, apiKey: "test-key");

        var standings = await client.GetTeamStandingsAsync(17, 76986);

        var arsenal = Assert.Single(standings);
        Assert.Equal(10, arsenal.Played);
        Assert.Equal(8, arsenal.Wins);
        Assert.Equal(1, arsenal.Draws);
        Assert.Equal(1, arsenal.Losses);
        Assert.Equal(24, arsenal.GoalsFor);
        Assert.Equal(7, arsenal.GoalsAgainst);
        Assert.Equal(17, arsenal.GoalDifference);
        Assert.Equal(25, arsenal.Points);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter GetTeamStandingsAsync_MapsFullClassicColumns`
Expected: FAIL — compile error, `FootballTeamStanding` has no `Played`/`Wins`/etc.

- [ ] **Step 3: Extend the standing record** — replace the whole body of `FootballTeamStanding.cs`:

```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed record FootballTeamStanding(
    int ProviderId,
    string Name,
    string Abbreviation,
    int Position,
    int Played,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int Points)
{
    public int GoalDifference => GoalsFor - GoalsAgainst;
}
```

- [ ] **Step 4: Parse the new row fields** — in `FootApiClient.cs`, replace the `.Select(...)` projection (lines 53-58) and the private `FootApiStandingRow` record (lines 135-138):

```csharp
            .Select(row => new FootballTeamStanding(
                row.Team!.Id,
                row.Team.Name ?? string.Empty,
                row.Team.NameCode ?? string.Empty,
                row.Position,
                row.Matches,
                row.Wins,
                row.Draws,
                row.Losses,
                row.ScoresFor,
                row.ScoresAgainst,
                row.Points))
```

```csharp
    private sealed record FootApiStandingRow(
        FootApiTeam? Team,
        int Position,
        int Matches,
        int Wins,
        int Draws,
        int Losses,
        int ScoresFor,
        int ScoresAgainst,
        int Points);
```

- [ ] **Step 5: Fix the TeamSyncService construction site** — `TeamSyncService.cs` only reads `ProviderId/Name/Abbreviation/Position/Points` properties (lines 104-108) and never constructs a `FootballTeamStanding`, so no change is needed there. Confirm by building.

- [ ] **Step 6: Run the new + existing client tests**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter FootApiClientTests`
Expected: PASS — including the pre-existing `GetTeamStandingsAsync_MapsFootApiRows` (missing fields default to 0).

- [ ] **Step 7: Commit**

```bash
git add backend/PLeagueHub.Api/Services/Football/FootballTeamStanding.cs backend/PLeagueHub.Api/Services/Football/FootApiClient.cs backend/PLeagueHub.Api.Tests/FootApiClientTests.cs
git commit -m "Parse full classic columns from FootApi standings"
```

---

## Task 2: Seasons-list provider method

**Files:**
- Create: `backend/PLeagueHub.Api/Services/Football/FootballSeason.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/FootApiClient.cs`
- Test: `backend/PLeagueHub.Api.Tests/FootApiClientTests.cs`

- [ ] **Step 1: Write the failing test** — append to `FootApiClientTests.cs`:

```csharp
    [Fact]
    public async Task GetSeasonsAsync_MapsSeasonsAndHitsTournamentEndpoint()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return JsonResponse("""
                {"seasons":[
                  {"id":96668,"name":"Premier League 26/27","year":"26/27"},
                  {"id":76986,"name":"Premier League 24/25","year":"24/25"}
                ]}
                """);
        });
        var client = CreateClient(handler, apiKey: "test-key");

        var seasons = await client.GetSeasonsAsync(17);

        Assert.Equal(
            "https://footapi7.p.rapidapi.com/api/tournament/17/seasons",
            capturedRequest!.RequestUri?.AbsoluteUri);
        Assert.Collection(seasons,
            first => { Assert.Equal(96668, first.Id); Assert.Equal("26/27", first.Year); },
            second => Assert.Equal(76986, second.Id));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter GetSeasonsAsync_MapsSeasonsAndHitsTournamentEndpoint`
Expected: FAIL — `IFootballProvider`/`FootApiClient` has no `GetSeasonsAsync`.

- [ ] **Step 3: Add the season record** — create `FootballSeason.cs`:

```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed record FootballSeason(int Id, string Name, string Year);
```

- [ ] **Step 4: Extend the interface** — add to `IFootballProvider.cs` (inside the interface):

```csharp
    Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(
        int tournamentId,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 5: Implement in FootApiClient** — add this method to `FootApiClient.cs` (after `GetTeamStandingsAsync`) and the two private records (next to the existing standings records):

```csharp
    public async Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(
        int tournamentId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest($"/api/tournament/{tournamentId}/seasons");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<FootApiSeasonsResponse>(
            responseStream,
            JsonOptions,
            cancellationToken);

        return payload?.Seasons?
            .Select(season => new FootballSeason(
                season.Id,
                season.Name ?? string.Empty,
                season.Year ?? string.Empty))
            .ToArray() ?? [];
    }
```

```csharp
    private sealed record FootApiSeasonsResponse(
        IReadOnlyCollection<FootApiSeason>? Seasons);

    private sealed record FootApiSeason(int Id, string? Name, string? Year);
```

- [ ] **Step 6: Run the test**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter GetSeasonsAsync_MapsSeasonsAndHitsTournamentEndpoint`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add backend/PLeagueHub.Api/Services/Football/FootballSeason.cs backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs backend/PLeagueHub.Api/Services/Football/FootApiClient.cs backend/PLeagueHub.Api.Tests/FootApiClientTests.cs
git commit -m "Add FootApi seasons lookup for the Premier League"
```

---

## Task 3: DTOs + cached StandingsService

**Files:**
- Create: `backend/PLeagueHub.Api/Responses/SeasonResponse.cs`
- Create: `backend/PLeagueHub.Api/Responses/StandingRowResponse.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/StandingsUnavailableException.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/StandingsService.cs`
- Test: `backend/PLeagueHub.Api.Tests/StandingsServiceTests.cs`

- [ ] **Step 1: Add the DTOs and exception**

`SeasonResponse.cs`:
```csharp
namespace PLeagueHub.Api.Responses;

public sealed record SeasonResponse(int SeasonId, string Label);
```

`StandingRowResponse.cs`:
```csharp
namespace PLeagueHub.Api.Responses;

public sealed record StandingRowResponse(
    int Position,
    int ProviderId,
    string Naziv,
    string Skracenica,
    string LogoUrl,
    int Odigrano,
    int Pobede,
    int Nereseno,
    int Porazi,
    int DatiGolovi,
    int PrimljeniGolovi,
    int GolRazlika,
    int Bodovi);
```

`StandingsUnavailableException.cs`:
```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed class StandingsUnavailableException : Exception
{
    public StandingsUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

- [ ] **Step 2: Write the failing test** — create `StandingsServiceTests.cs`:

```csharp
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class StandingsServiceTests
{
    [Fact]
    public async Task GetStandingsAsync_OrdersByPointsThenGoalDifferenceAndJoinsLogos()
    {
        var provider = new FakeFootballProvider
        {
            Standings =
            [
                new FootballTeamStanding(42, "Arsenal", "ARS", 2, 10, 7, 1, 2, 20, 10, 22),
                new FootballTeamStanding(60, "City", "MCI", 1, 10, 7, 1, 2, 25, 8, 22)
            ]
        };
        var teams = new FakeTeamRepository(
        [
            new Team { Id = "t1", ProviderId = 60, Naziv = "City", LogoUrl = "/city.png" }
        ]);
        var service = new StandingsService(provider, teams, new MemoryCache(new MemoryCacheOptions()));

        var rows = await service.GetStandingsAsync(96668, CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Equal("City", rows.First().Naziv);           // higher GD wins the tie
        Assert.Equal("/city.png", rows.First().LogoUrl);    // logo joined by ProviderId
        Assert.Equal(17, rows.First().GolRazlika);
        Assert.Equal(string.Empty, rows.Last().LogoUrl);    // no Team match -> empty
    }

    [Fact]
    public async Task GetStandingsAsync_CachesProviderResultPerSeason()
    {
        var provider = new FakeFootballProvider
        {
            Standings = [new FootballTeamStanding(42, "Arsenal", "ARS", 1, 1, 1, 0, 0, 3, 0, 3)]
        };
        var service = new StandingsService(
            provider, new FakeTeamRepository([]), new MemoryCache(new MemoryCacheOptions()));

        await service.GetStandingsAsync(96668, CancellationToken.None);
        await service.GetStandingsAsync(96668, CancellationToken.None);

        Assert.Equal(1, provider.StandingsCallCount);
    }

    [Fact]
    public async Task GetStandingsAsync_WrapsProviderFailure()
    {
        var provider = new FakeFootballProvider { ThrowOnStandings = true };
        var service = new StandingsService(
            provider, new FakeTeamRepository([]), new MemoryCache(new MemoryCacheOptions()));

        await Assert.ThrowsAsync<StandingsUnavailableException>(
            () => service.GetStandingsAsync(96668, CancellationToken.None));
    }

    [Fact]
    public async Task GetSeasonsAsync_MapsLabelsFromYear()
    {
        var provider = new FakeFootballProvider
        {
            Seasons = [new FootballSeason(96668, "Premier League 26/27", "26/27")]
        };
        var service = new StandingsService(
            provider, new FakeTeamRepository([]), new MemoryCache(new MemoryCacheOptions()));

        var seasons = await service.GetSeasonsAsync(CancellationToken.None);

        var season = Assert.Single(seasons);
        Assert.Equal(96668, season.SeasonId);
        Assert.Equal("26/27", season.Label);
    }

    [Fact]
    public async Task GetSeasonsAsync_FallsBackToCurrentSeasonOnFailure()
    {
        var provider = new FakeFootballProvider { ThrowOnSeasons = true };
        var service = new StandingsService(
            provider, new FakeTeamRepository([]), new MemoryCache(new MemoryCacheOptions()));

        var seasons = await service.GetSeasonsAsync(CancellationToken.None);

        Assert.Equal(96668, Assert.Single(seasons).SeasonId);
    }

    private sealed class FakeFootballProvider : IFootballProvider
    {
        public IReadOnlyCollection<FootballTeamStanding> Standings { get; set; } = [];
        public IReadOnlyCollection<FootballSeason> Seasons { get; set; } = [];
        public bool ThrowOnStandings { get; set; }
        public bool ThrowOnSeasons { get; set; }
        public int StandingsCallCount { get; private set; }

        public Task<JsonDocument> SearchAsync(string term, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FootballTeamLogo> GetTeamLogoAsync(int providerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(
            int tournamentId, int seasonId, CancellationToken cancellationToken = default)
        {
            StandingsCallCount++;
            if (ThrowOnStandings)
            {
                throw new HttpRequestException("boom");
            }

            return Task.FromResult(Standings);
        }

        public Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(
            int tournamentId, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSeasons)
            {
                throw new HttpRequestException("boom");
            }

            return Task.FromResult(Seasons);
        }
    }

    private sealed class FakeTeamRepository : IRepository<Team>
    {
        private readonly List<Team> _teams;
        public FakeTeamRepository(IEnumerable<Team> teams) => _teams = teams.ToList();

        public Task<IReadOnlyCollection<Team>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<Team>>(_teams);

        public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.FirstOrDefault(team => team.Id == id));

        public Task<Team?> FindOneAsync(
            Expression<Func<Team, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.FirstOrDefault(predicate.Compile()));

        public Task<bool> ExistsAsync(
            Expression<Func<Team, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(_teams.Any(predicate.Compile()));

        public Task<Team> CreateAsync(Team entity, CancellationToken cancellationToken = default)
            => Task.FromResult(entity);

        public Task<bool> UpdateAsync(string id, Team entity, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
```

> **Implementer note:** `IRepository<TDocument>` is constrained `where TDocument : BaseDocument` — `Team` already satisfies this. The fake above implements all seven interface members.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter StandingsServiceTests`
Expected: FAIL — `StandingsService` does not exist.

- [ ] **Step 4: Implement the service** — create `StandingsService.cs`:

```csharp
using Microsoft.Extensions.Caching.Memory;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IStandingsService
{
    Task<IReadOnlyCollection<SeasonResponse>> GetSeasonsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StandingRowResponse>> GetStandingsAsync(
        int seasonId,
        CancellationToken cancellationToken);
}

public sealed class StandingsService : IStandingsService
{
    public const int PremierLeagueTournamentId = 17;
    public const int CurrentSeasonId = 96668;

    private static readonly TimeSpan CurrentSeasonTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PastSeasonTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SeasonsTtl = TimeSpan.FromHours(24);

    private readonly IFootballProvider _footballProvider;
    private readonly IRepository<Team> _teamsRepository;
    private readonly IMemoryCache _cache;

    public StandingsService(
        IFootballProvider footballProvider,
        IRepository<Team> teamsRepository,
        IMemoryCache cache)
    {
        _footballProvider = footballProvider;
        _teamsRepository = teamsRepository;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<SeasonResponse>> GetSeasonsAsync(
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue("standings:seasons", out IReadOnlyCollection<SeasonResponse>? cached)
            && cached is not null)
        {
            return cached;
        }

        IReadOnlyCollection<SeasonResponse> seasons;

        try
        {
            var providerSeasons = await _footballProvider.GetSeasonsAsync(
                PremierLeagueTournamentId,
                cancellationToken);

            seasons = providerSeasons
                .Select(season => new SeasonResponse(
                    season.Id,
                    string.IsNullOrWhiteSpace(season.Year) ? season.Name : season.Year))
                .ToArray();
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException
                or System.Text.Json.JsonException)
        {
            seasons = [new SeasonResponse(CurrentSeasonId, "Trenutna sezona")];
        }

        if (seasons.Count == 0)
        {
            seasons = [new SeasonResponse(CurrentSeasonId, "Trenutna sezona")];
        }

        _cache.Set("standings:seasons", seasons, SeasonsTtl);
        return seasons;
    }

    public async Task<IReadOnlyCollection<StandingRowResponse>> GetStandingsAsync(
        int seasonId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"standings:{seasonId}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyCollection<StandingRowResponse>? cached)
            && cached is not null)
        {
            return cached;
        }

        IReadOnlyCollection<FootballTeamStanding> standings;

        try
        {
            standings = await _footballProvider.GetTeamStandingsAsync(
                PremierLeagueTournamentId,
                seasonId,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException
                or System.Text.Json.JsonException)
        {
            throw new StandingsUnavailableException(
                "FootAPI standings could not be loaded.",
                exception);
        }

        var teams = await _teamsRepository.GetAllAsync(cancellationToken);
        var logoByProviderId = teams
            .Where(team => team.ProviderId is not null)
            .GroupBy(team => team.ProviderId!.Value)
            .ToDictionary(group => group.Key, group => group.First().LogoUrl);

        var rows = standings
            .OrderByDescending(standing => standing.Points)
            .ThenByDescending(standing => standing.GoalDifference)
            .ThenByDescending(standing => standing.GoalsFor)
            .Select((standing, index) => new StandingRowResponse(
                index + 1,
                standing.ProviderId,
                standing.Name,
                standing.Abbreviation,
                logoByProviderId.GetValueOrDefault(standing.ProviderId, string.Empty),
                standing.Played,
                standing.Wins,
                standing.Draws,
                standing.Losses,
                standing.GoalsFor,
                standing.GoalsAgainst,
                standing.GoalDifference,
                standing.Points))
            .ToArray();

        _cache.Set(cacheKey, rows, seasonId == CurrentSeasonId ? CurrentSeasonTtl : PastSeasonTtl);
        return rows;
    }
}
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter StandingsServiceTests`
Expected: PASS (all 5)

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Responses/SeasonResponse.cs backend/PLeagueHub.Api/Responses/StandingRowResponse.cs backend/PLeagueHub.Api/Services/Football/StandingsUnavailableException.cs backend/PLeagueHub.Api/Services/Football/StandingsService.cs backend/PLeagueHub.Api.Tests/StandingsServiceTests.cs
git commit -m "Add cached standings service with logo join and ordering"
```

---

## Task 4: StandingsController + DI registration

**Files:**
- Create: `backend/PLeagueHub.Api/Controllers/StandingsController.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs:74` (registrations)
- Test: `backend/PLeagueHub.Api.Tests/StandingsControllerTests.cs`

- [ ] **Step 1: Write the failing test** — create `StandingsControllerTests.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Controllers;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class StandingsControllerTests
{
    [Fact]
    public async Task GetStandings_ReturnsRowsFromService()
    {
        var service = new FakeStandingsService
        {
            Rows = [new StandingRowResponse(1, 60, "City", "MCI", "/city.png", 10, 8, 1, 1, 25, 8, 17, 25)]
        };
        var controller = new StandingsController(service);

        var result = await controller.GetStandingsAsync(96668, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var rows = Assert.IsAssignableFrom<IReadOnlyCollection<StandingRowResponse>>(ok.Value);
        Assert.Equal(96668, service.RequestedSeasonId);
        Assert.Single(rows);
    }

    [Fact]
    public async Task GetStandings_Returns502WhenProviderUnavailable()
    {
        var service = new FakeStandingsService { ThrowUnavailable = true };
        var controller = new StandingsController(service);

        var result = await controller.GetStandingsAsync(96668, CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, status.StatusCode);
    }

    [Fact]
    public async Task GetSeasons_ReturnsServiceSeasons()
    {
        var service = new FakeStandingsService
        {
            Seasons = [new SeasonResponse(96668, "26/27")]
        };
        var controller = new StandingsController(service);

        var result = await controller.GetSeasonsAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var seasons = Assert.IsAssignableFrom<IReadOnlyCollection<SeasonResponse>>(ok.Value);
        Assert.Single(seasons);
    }

    private sealed class FakeStandingsService : IStandingsService
    {
        public IReadOnlyCollection<StandingRowResponse> Rows { get; set; } = [];
        public IReadOnlyCollection<SeasonResponse> Seasons { get; set; } = [];
        public bool ThrowUnavailable { get; set; }
        public int RequestedSeasonId { get; private set; }

        public Task<IReadOnlyCollection<SeasonResponse>> GetSeasonsAsync(CancellationToken cancellationToken)
            => Task.FromResult(Seasons);

        public Task<IReadOnlyCollection<StandingRowResponse>> GetStandingsAsync(
            int seasonId, CancellationToken cancellationToken)
        {
            RequestedSeasonId = seasonId;
            if (ThrowUnavailable)
            {
                throw new StandingsUnavailableException("down", new HttpRequestException());
            }

            return Task.FromResult(Rows);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter StandingsControllerTests`
Expected: FAIL — `StandingsController` does not exist.

- [ ] **Step 3: Implement the controller** — create `StandingsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/standings")]
public sealed class StandingsController : ControllerBase
{
    private readonly IStandingsService _standingsService;

    public StandingsController(IStandingsService standingsService)
    {
        _standingsService = standingsService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<StandingRowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyCollection<StandingRowResponse>>> GetStandingsAsync(
        [FromQuery] int seasonId = StandingsService.CurrentSeasonId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _standingsService.GetStandingsAsync(seasonId, cancellationToken);
            return Ok(rows);
        }
        catch (StandingsUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
    }

    [HttpGet("seasons")]
    [ProducesResponseType(typeof(IReadOnlyCollection<SeasonResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<SeasonResponse>>> GetSeasonsAsync(
        CancellationToken cancellationToken = default)
    {
        var seasons = await _standingsService.GetSeasonsAsync(cancellationToken);
        return Ok(seasons);
    }
}
```

- [ ] **Step 4: Register the service + memory cache** — in `Program.cs`, immediately before `builder.Services.AddControllers();` (line 74) add:

```csharp
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IStandingsService, StandingsService>();
```

- [ ] **Step 5: Run the controller tests + full build**

Run: `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter StandingsControllerTests`
Expected: PASS (all 3)
Run: `dotnet build backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add backend/PLeagueHub.Api/Controllers/StandingsController.cs backend/PLeagueHub.Api/Program.cs backend/PLeagueHub.Api.Tests/StandingsControllerTests.cs
git commit -m "Expose public standings endpoints with 502 fallback"
```

---

## Task 5: Frontend types + standingsApi

**Files:**
- Modify: `frontend/src/types/api.ts`
- Create: `frontend/src/services/standingsApi.ts`
- Test: `frontend/src/services/standingsApi.test.ts`

- [ ] **Step 1: Add the types** — append to `frontend/src/types/api.ts`:

```ts
export interface Season {
  seasonId: number;
  label: string;
}

export interface StandingRow {
  position: number;
  providerId: number;
  naziv: string;
  skracenica: string;
  logoUrl: string;
  odigrano: number;
  pobede: number;
  nereseno: number;
  porazi: number;
  datiGolovi: number;
  primljeniGolovi: number;
  golRazlika: number;
  bodovi: number;
}
```

- [ ] **Step 2: Write the failing test** — create `frontend/src/services/standingsApi.test.ts`:

```ts
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { get } = vi.hoisted(() => ({ get: vi.fn().mockResolvedValue({ data: [] }) }));
vi.mock('./api', () => ({ api: { get } }));

import { standingsApi } from './standingsApi';

describe('standingsApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('requests the seasons list', () => {
    standingsApi.getSeasons();
    expect(get).toHaveBeenCalledWith('/api/standings/seasons');
  });

  it('requests standings for a given season', () => {
    standingsApi.getStandings(76986);
    expect(get).toHaveBeenCalledWith('/api/standings', { params: { seasonId: 76986 } });
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd frontend && npm test -- standingsApi`
Expected: FAIL — cannot find `./standingsApi`.

- [ ] **Step 4: Implement the service** — create `frontend/src/services/standingsApi.ts`:

```ts
import type { Season, StandingRow } from '../types/api';
import { api } from './api';

export const standingsApi = {
  async getSeasons() {
    const response = await api.get<Season[]>('/api/standings/seasons');
    return response.data;
  },
  async getStandings(seasonId: number) {
    const response = await api.get<StandingRow[]>('/api/standings', {
      params: { seasonId }
    });
    return response.data;
  }
};
```

- [ ] **Step 5: Run the test**

Run: `cd frontend && npm test -- standingsApi`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add frontend/src/types/api.ts frontend/src/services/standingsApi.ts frontend/src/services/standingsApi.test.ts
git commit -m "Add standings API client and types"
```

---

## Task 6: TablePage

**Files:**
- Create: `frontend/src/pages/Table.tsx`
- Test: `frontend/src/pages/Table.test.tsx`

- [ ] **Step 1: Write the failing test** — create `frontend/src/pages/Table.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const { getSeasons, getStandings } = vi.hoisted(() => ({
  getSeasons: vi.fn(),
  getStandings: vi.fn()
}));
vi.mock('../services/standingsApi', () => ({ standingsApi: { getSeasons, getStandings } }));

import { TablePage } from './Table';

const row = {
  position: 1, providerId: 60, naziv: 'Manchester City', skracenica: 'MCI', logoUrl: '',
  odigrano: 10, pobede: 8, nereseno: 1, porazi: 1, datiGolovi: 25, primljeniGolovi: 8,
  golRazlika: 17, bodovi: 25
};

describe('TablePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getSeasons.mockResolvedValue([
      { seasonId: 96668, label: '26/27' },
      { seasonId: 76986, label: '24/25' }
    ]);
    getStandings.mockResolvedValue([row]);
  });

  it('renders standings rows for the default season', async () => {
    render(<TablePage />);
    expect(await screen.findByText('Manchester City')).toBeInTheDocument();
    expect(getStandings).toHaveBeenCalledWith(96668);
  });

  it('refetches when a different season is selected', async () => {
    render(<TablePage />);
    await screen.findByText('Manchester City');

    await userEvent.selectOptions(screen.getByLabelText('Sezona'), '76986');

    await waitFor(() => expect(getStandings).toHaveBeenLastCalledWith(76986));
  });

  it('shows an error state when standings fail to load', async () => {
    getStandings.mockRejectedValue(new Error('boom'));
    render(<TablePage />);
    expect(await screen.findByText(/nije moguce ucitati tabelu/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd frontend && npm test -- Table`
Expected: FAIL — cannot find `./Table`.

- [ ] **Step 3: Implement the page** — create `frontend/src/pages/Table.tsx`:

```tsx
import { ListOrdered } from 'lucide-react';
import { useEffect, useState } from 'react';
import { standingsApi } from '../services/standingsApi';
import type { Season, StandingRow } from '../types/api';
import { TeamLogo } from '../components/TeamLogo';

const FALLBACK_SEASON: Season = { seasonId: 96668, label: 'Trenutna sezona' };

export function TablePage() {
  const [seasons, setSeasons] = useState<Season[]>([]);
  const [seasonId, setSeasonId] = useState<number | null>(null);
  const [rows, setRows] = useState<StandingRow[]>([]);
  const [status, setStatus] = useState<'loading' | 'error' | 'ready'>('loading');

  useEffect(() => {
    standingsApi.getSeasons()
      .then((data) => {
        const list = data.length > 0 ? data : [FALLBACK_SEASON];
        setSeasons(list);
        setSeasonId(list[0].seasonId);
      })
      .catch(() => {
        setSeasons([FALLBACK_SEASON]);
        setSeasonId(FALLBACK_SEASON.seasonId);
      });
  }, []);

  useEffect(() => {
    if (seasonId === null) {
      return;
    }

    setStatus('loading');
    standingsApi.getStandings(seasonId)
      .then((data) => {
        setRows(data);
        setStatus('ready');
      })
      .catch(() => setStatus('error'));
  }, [seasonId]);

  return (
    <div className="space-y-4">
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="flex items-center gap-1 text-[10px] font-bold uppercase text-brand">
              <ListOrdered size={13} /> Premier League
            </p>
            <h1 className="mt-1 text-xl font-extrabold">Tabela</h1>
          </div>
          <label className="flex flex-col text-xs font-semibold text-slate-500">
            Sezona
            <select
              aria-label="Sezona"
              className="mt-1 rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 outline-none focus:border-brand"
              value={seasonId ?? ''}
              onChange={(event) => setSeasonId(Number(event.target.value))}
            >
              {seasons.map((season) => (
                <option key={season.seasonId} value={season.seasonId}>{season.label}</option>
              ))}
            </select>
          </label>
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm">
        {status === 'loading' && (
          <p className="px-4 py-8 text-center text-sm text-slate-400">Ucitavanje tabele...</p>
        )}
        {status === 'error' && (
          <p className="px-4 py-8 text-center text-sm text-red-500">
            Trenutno nije moguce ucitati tabelu. Pokusaj ponovo kasnije.
          </p>
        )}
        {status === 'ready' && rows.length === 0 && (
          <p className="px-4 py-8 text-center text-sm text-slate-400">Nema podataka za izabranu sezonu.</p>
        )}
        {status === 'ready' && rows.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] text-left text-sm">
              <thead className="bg-ink text-[10px] uppercase text-slate-300">
                <tr>
                  <th className="px-3 py-3">#</th>
                  <th className="px-3 py-3">Klub</th>
                  <th className="px-3 py-3 text-center">OD</th>
                  <th className="px-3 py-3 text-center">P</th>
                  <th className="px-3 py-3 text-center">N</th>
                  <th className="px-3 py-3 text-center">I</th>
                  <th className="hidden px-3 py-3 text-center sm:table-cell">GF:GA</th>
                  <th className="hidden px-3 py-3 text-center sm:table-cell">GR</th>
                  <th className="px-3 py-3 text-right">Bod</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((team) => (
                  <tr key={team.providerId} className="border-b border-slate-100 hover:bg-slate-50">
                    <td className="px-3 py-3 text-xs font-bold text-slate-400">{team.position}</td>
                    <td className="px-3 py-3">
                      <div className="flex items-center gap-2">
                        <TeamLogo className="size-6" logoUrl={team.logoUrl} name={team.naziv} />
                        <span className="text-xs font-semibold">{team.naziv}</span>
                      </div>
                    </td>
                    <td className="px-3 py-3 text-center text-xs">{team.odigrano}</td>
                    <td className="px-3 py-3 text-center text-xs">{team.pobede}</td>
                    <td className="px-3 py-3 text-center text-xs">{team.nereseno}</td>
                    <td className="px-3 py-3 text-center text-xs">{team.porazi}</td>
                    <td className="hidden px-3 py-3 text-center text-xs sm:table-cell">
                      {team.datiGolovi}:{team.primljeniGolovi}
                    </td>
                    <td className="hidden px-3 py-3 text-center text-xs font-semibold sm:table-cell">
                      {team.golRazlika > 0 ? `+${team.golRazlika}` : team.golRazlika}
                    </td>
                    <td className="px-3 py-3 text-right font-black">{team.bodovi}</td>
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

- [ ] **Step 4: Run the tests**

Run: `cd frontend && npm test -- Table`
Expected: PASS (all 3)

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/Table.tsx frontend/src/pages/Table.test.tsx
git commit -m "Add standings table page with season selector"
```

---

## Task 7: Wire route + nav item

**Files:**
- Modify: `frontend/src/App.tsx:13,21`
- Modify: `frontend/src/components/Layout.tsx:1-12,22-28`

- [ ] **Step 1: Add the route** — in `App.tsx`, add the import after the `Stats` import (line 13):

```tsx
import { Stats } from './pages/Stats';
import { TablePage } from './pages/Table';
```

and add the route immediately after the `stats` route (line 21):

```tsx
        <Route path="stats" element={<Stats />} />
        <Route path="tabela" element={<TablePage />} />
```

- [ ] **Step 2: Add the nav item** — in `Layout.tsx`, add `ListOrdered` to the lucide import block (lines 2-12, keep alphabetical-ish ordering):

```tsx
import {
  BarChart3,
  CalendarDays,
  Home,
  ListOrdered,
  LogIn,
  LogOut,
  MessagesSquare,
  Newspaper,
  Star,
  Trophy
} from 'lucide-react';
```

and insert the nav entry between Statistike and Vesti in `navItems` (line 25):

```tsx
const navItems = [
  { to: '/', label: 'Pocetna', icon: Home },
  { to: '/results', label: 'Rezultati', icon: CalendarDays },
  { to: '/stats', label: 'Statistike', icon: BarChart3 },
  { to: '/tabela', label: 'Tabela', icon: ListOrdered },
  { to: '/news', label: 'Vesti', icon: Newspaper },
  { to: '/forum', label: 'Forum', icon: MessagesSquare }
];
```

- [ ] **Step 3: Type-check + full frontend suite**

Run: `cd frontend && npm run build`
Expected: `tsc --noEmit` passes, Vite build succeeds.
Run: `cd frontend && npm test`
Expected: all suites pass (existing 54 + new standingsApi + Table tests).

- [ ] **Step 4: Commit**

```bash
git add frontend/src/App.tsx frontend/src/components/Layout.tsx
git commit -m "Add Tabela nav entry and route"
```

---

## Final Verification (verification-before-completion)

- [ ] **Backend:** `dotnet build backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj` → 0 errors; `dotnet test ... --filter "FootApiClientTests|StandingsServiceTests|StandingsControllerTests"` → all green (no MongoDB needed).
- [ ] **Frontend:** `cd frontend && npm test` → all green; `npm run build` → succeeds.
- [ ] **Manual (requires Docker MongoDB up + `FootApi:ApiKey` in user secrets):** start API + frontend, click "Tabela" between Statistike and Vesti, confirm full table renders for the current season, switch to a past season, confirm it reloads. Verify mobile width hides GF:GA + GR.
- [ ] **Assumption check:** confirm the live FootApi `standings/total` rows actually contain `matches/wins/draws/losses/scoresFor/scoresAgainst` and that `/api/tournament/17/seasons` returns the season list. If field names differ, adjust `FootApiStandingRow` / `FootApiSeason` to match.

## Out of Scope (YAGNI)
Persisting standings in MongoDB; live auto-refresh; reconciling the sidebar mini-widget; per-team form/last-5.
