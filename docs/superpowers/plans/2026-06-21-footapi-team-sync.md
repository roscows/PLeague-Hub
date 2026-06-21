# FootAPI Team Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Import Premier League teams and standings from FootAPI into MongoDB without changing existing Mongo document IDs.

**Architecture:** `FootApiClient` maps provider JSON into provider-neutral standing records. `TeamSyncService` validates the complete response and idempotently creates, updates, or skips `Team` documents through `IRepository<Team>`. An administrator-only controller exposes one explicit sync command in Swagger.

**Tech Stack:** .NET 10, ASP.NET Core controllers, MongoDB.Driver, HttpClient, System.Text.Json, JWT role authorization, xUnit

---

### Task 1: Provider Standings Contract

**Files:**
- Create: `backend/PLeagueHub.Api/Services/Football/FootballTeamStanding.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/FootApiClient.cs`
- Test: `backend/PLeagueHub.Api.Tests/FootApiClientTests.cs`

- [ ] **Step 1: Write the failing standings-client test**

Add a test that returns the real FootAPI response shape and expects a provider-neutral result:

```csharp
[Fact]
public async Task GetTeamStandingsAsync_MapsFootApiRows()
{
    HttpRequestMessage? capturedRequest = null;
    var handler = new StubHttpMessageHandler(request =>
    {
        capturedRequest = request;
        return JsonResponse("""
            {"standings":[{"rows":[
              {"team":{"id":42,"name":"Arsenal","nameCode":"ARS"},"position":1,"points":85}
            ]}]}
            """);
    });
    var client = CreateClient(handler, "test-key");

    var standings = await client.GetTeamStandingsAsync(17, 76986);

    Assert.Equal("https://footapi7.p.rapidapi.com/api/tournament/17/season/76986/standings/total",
        capturedRequest!.RequestUri!.AbsoluteUri);
    var arsenal = Assert.Single(standings);
    Assert.Equal(42, arsenal.ProviderId);
    Assert.Equal("Arsenal", arsenal.Name);
    Assert.Equal("ARS", arsenal.Abbreviation);
    Assert.Equal(1, arsenal.Position);
    Assert.Equal(85, arsenal.Points);
}
```

Add `JsonResponse` beside the existing test helper:

```csharp
private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
{
    Content = new StringContent(json, Encoding.UTF8, "application/json")
};
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
dotnet test backend\PLeagueHub.Api.Tests\PLeagueHub.Api.Tests.csproj --filter FullyQualifiedName~GetTeamStandingsAsync_MapsFootApiRows --no-restore
```

Expected: compilation fails because `GetTeamStandingsAsync` does not exist.

- [ ] **Step 3: Add the provider-neutral record and interface method**

Create:

```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed record FootballTeamStanding(
    int ProviderId,
    string Name,
    string Abbreviation,
    int Position,
    int Points);
```

Add to `IFootballProvider`:

```csharp
Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(
    int tournamentId,
    int seasonId,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 4: Implement minimal FootAPI deserialization**

Add `GetTeamStandingsAsync` to `FootApiClient`. Reuse a private `CreateRequest` method so both search and standings requests receive the RapidAPI headers. Deserialize these private DTOs with `JsonSerializerOptions.Web`:

```csharp
private sealed record FootApiStandingsResponse(IReadOnlyCollection<FootApiStandingGroup>? Standings);
private sealed record FootApiStandingGroup(IReadOnlyCollection<FootApiStandingRow>? Rows);
private sealed record FootApiStandingRow(FootApiTeam? Team, int Position, int Points);
private sealed record FootApiTeam(int Id, string? Name, string? NameCode);
```

Map every row with a non-null team to `FootballTeamStanding`; do not validate business fields in the HTTP client.

- [ ] **Step 5: Run all FootApi client tests and verify GREEN**

```powershell
dotnet test backend\PLeagueHub.Api.Tests\PLeagueHub.Api.Tests.csproj --filter FullyQualifiedName~FootApiClientTests --no-restore
```

Expected: all `FootApiClientTests` pass.

### Task 2: Idempotent Team Synchronization

**Files:**
- Modify: `backend/PLeagueHub.Api/Models/Team.cs`
- Create: `backend/PLeagueHub.Api/Responses/TeamSyncResponse.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/TeamSyncException.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/TeamSyncService.cs`
- Test: `backend/PLeagueHub.Api.Tests/TeamSyncServiceTests.cs`

- [ ] **Step 1: Write failing tests for matching and preservation**

Use an in-memory `IRepository<Team>` and a fake `IFootballProvider`. Cover these concrete cases:

```csharp
[Fact]
public async Task SyncAsync_MatchesSeedTeamByNameAndPreservesProfileFields()
{
    var existing = new Team
    {
        Id = "665000000000000000000001", Naziv = " Arsenal ", Skracenica = "OLD",
        Stadion = "Emirates Stadium", Osnovan = 1886, LogoUrl = "arsenal.svg",
        Bodovi = 1, Pozicija = 20
    };
    var repository = new FakeTeamRepository([existing]);
    var service = CreateService(repository,
        [new FootballTeamStanding(42, "Arsenal", "ARS", 1, 85)]);

    var result = await service.SyncAsync(76986);

    Assert.Equal(1, result.Updated);
    Assert.Equal(42, existing.ProviderId);
    Assert.Equal("Emirates Stadium", existing.Stadion);
    Assert.Equal(1886, existing.Osnovan);
    Assert.Equal("arsenal.svg", existing.LogoUrl);
    Assert.Equal(85, existing.Bodovi);
}
```

Also add tests named:

```text
SyncAsync_MatchesByProviderIdBeforeName
SyncAsync_CreatesMissingTeam
SyncAsync_SkipsUnchangedTeam
SyncAsync_RejectsEmptyStandingsWithoutWrites
SyncAsync_RejectsDuplicateProviderIdsWithoutWrites
```

- [ ] **Step 2: Run the service tests and verify RED**

```powershell
dotnet test backend\PLeagueHub.Api.Tests\PLeagueHub.Api.Tests.csproj --filter FullyQualifiedName~TeamSyncServiceTests --no-restore
```

Expected: compilation fails because the sync types do not exist.

- [ ] **Step 3: Add external ID and result types**

Add to `Team`:

```csharp
[BsonElement("provider_id")]
[BsonIgnoreIfNull]
public int? ProviderId { get; set; }
```

Create:

```csharp
namespace PLeagueHub.Api.Responses;

public sealed record TeamSyncResponse(
    int TournamentId,
    int SeasonId,
    int Created,
    int Updated,
    int Skipped);
```

Create a focused exception:

```csharp
namespace PLeagueHub.Api.Services.Football;

public sealed class TeamSyncException(string message, Exception? innerException = null)
    : Exception(message, innerException);
```

- [ ] **Step 4: Implement `TeamSyncService`**

Use fixed tournament ID `17`. Fetch and validate all rows before calling the repository. Validation requires a non-empty collection, positive unique provider IDs, non-blank names, positive positions, and non-negative points.

Build dictionaries from one `GetAllAsync` call. Match first by `ProviderId`, then by normalized name only when the existing team has no provider ID. Normalization trims, collapses whitespace, and uses `ToUpperInvariant()`.

For matched teams, preserve `Id`, `Stadion`, `Osnovan`, and `LogoUrl`. Skip writes when provider ID, name, abbreviation, position, and points are already equal. Throw `TeamSyncException` if `UpdateAsync` returns false. Create missing teams with blank profile fields.

Return:

```csharp
return new TeamSyncResponse(17, seasonId, created, updated, skipped);
```

Wrap `HttpRequestException`, `JsonException`, and provider configuration `InvalidOperationException` from the provider call in `TeamSyncException("FootAPI standings could not be loaded.", exception)`.

- [ ] **Step 5: Run service tests and verify GREEN**

```powershell
dotnet test backend\PLeagueHub.Api.Tests\PLeagueHub.Api.Tests.csproj --filter FullyQualifiedName~TeamSyncServiceTests --no-restore
```

Expected: all `TeamSyncServiceTests` pass.

### Task 3: MongoDB Provider ID Index

**Files:**
- Modify: `backend/PLeagueHub.Api/Data/MongoIndexInitializer.cs`

- [ ] **Step 1: Add the sparse unique index**

Append this index to `CreateTeamIndexesAsync`:

```csharp
new CreateIndexModel<Team>(
    Builders<Team>.IndexKeys.Ascending(team => team.ProviderId),
    new CreateIndexOptions
    {
        Name = "idx_teams_provider_id",
        Unique = true,
        Sparse = true
    })
```

- [ ] **Step 2: Start the API once to verify index creation against Docker MongoDB**

```powershell
dotnet run --project backend\PLeagueHub.Api\PLeagueHub.Api.csproj --no-build
```

Expected: API starts without a Mongo index exception. Stop it after startup verification.

### Task 4: Administrator Swagger Endpoint

**Files:**
- Create: `backend/PLeagueHub.Api/Controllers/IntegrationsController.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test: `backend/PLeagueHub.Api.Tests/IntegrationEndpointsTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Create a `WebApplicationFactory<Program>` that replaces `IFootballProvider` and `IRepository<Team>` with deterministic fakes. Add tests for:

```text
SyncTeams_ReturnsUnauthorized_WhenTokenIsMissing
SyncTeams_ReturnsForbidden_WhenRoleIsModerator
SyncTeams_ReturnsBadRequest_WhenSeasonIdIsNotPositive
SyncTeams_ReturnsSummary_WhenAdministratorRunsSync
SyncTeams_ReturnsBadGateway_WhenProviderFails
```

The success assertion must deserialize `TeamSyncResponse` and verify tournament `17`, requested season, and create/update/skip counts.

- [ ] **Step 2: Run endpoint tests and verify RED**

```powershell
dotnet test backend\PLeagueHub.Api.Tests\PLeagueHub.Api.Tests.csproj --filter FullyQualifiedName~IntegrationEndpointsTests --no-restore
```

Expected: `404 Not Found` or compilation failure because the controller and service registration do not exist.

- [ ] **Step 3: Register the service**

Add to `Program.cs` beside `SearchService`:

```csharp
builder.Services.AddScoped<TeamSyncService>();
```

- [ ] **Step 4: Implement the controller**

Create an `[ApiController]`, `[Authorize(Roles = "administrator")]`, and `[Route("api/integrations/football")]` controller. Implement:

```csharp
[HttpPost("sync/teams")]
[ProducesResponseType(typeof(TeamSyncResponse), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status502BadGateway)]
public async Task<ActionResult<TeamSyncResponse>> SyncTeamsAsync(
    [FromQuery] int seasonId = 96668,
    CancellationToken cancellationToken = default)
{
    if (seasonId <= 0)
    {
        return BadRequest(new { message = "seasonId must be greater than zero." });
    }

    try
    {
        return Ok(await _teamSyncService.SyncAsync(seasonId, cancellationToken));
    }
    catch (TeamSyncException exception)
    {
        return StatusCode(StatusCodes.Status502BadGateway, new { message = exception.Message });
    }
}
```

- [ ] **Step 5: Run endpoint tests and verify GREEN**

```powershell
dotnet test backend\PLeagueHub.Api.Tests\PLeagueHub.Api.Tests.csproj --filter FullyQualifiedName~IntegrationEndpointsTests --no-restore
```

Expected: all integration endpoint tests pass.

### Task 5: Regression and Live Verification

**Files:**
- Modify only if verification reveals a defect in files already listed above.

- [ ] **Step 1: Run the complete backend suite**

```powershell
dotnet test backend\PLeagueHub.Api.Tests\PLeagueHub.Api.Tests.csproj --no-restore
```

Expected: zero failed tests.

- [ ] **Step 2: Verify formatting and secret safety**

```powershell
git diff --check
git grep -n "X-RapidAPI-Key"
```

Expected: no whitespace errors; only header-name code references, never the real key.

- [ ] **Step 3: Run one real sync through the API**

Start the API, authenticate as the seeded administrator through Swagger, authorize with the returned JWT, and execute:

```http
POST /api/integrations/football/sync/teams?seasonId=96668
```

Expected: `200 OK`, 20 Premier League teams represented in MongoDB, and no duplicate `provider_id` values. A second execution must report all unchanged teams as skipped.

- [ ] **Step 4: Inspect MongoDB results**

```powershell
docker exec pleaguehub-mongodb mongosh --quiet --eval "db.getSiblingDB('PLeagueHubDb').Teams.find({}, {naziv:1, provider_id:1, pozicija:1, bodovi:1}).sort({pozicija:1}).toArray()"
```

Expected: provider IDs are populated, `_id` remains MongoDB-owned, and standings fields match the provider response.
