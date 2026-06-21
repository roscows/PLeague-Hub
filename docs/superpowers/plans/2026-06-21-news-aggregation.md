# News Aggregation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a free live Premier League news timeline with safe RSS/Atom ingestion, manual X embeds, editorial controls, reliability labels, deduplication, internal details, and moderated comments.

**Architecture:** Extend the existing `Posts` collection for news metadata, add a dedicated `NewsSources` collection, and isolate fetching, parsing, filtering, deduplication, and scheduling behind focused backend services. Reuse the established comment/vote moderation behavior through a shared comment service, then replace the current flat News page with the approved timeline, detail page, editor, and source-management view.

**Tech Stack:** .NET 10, ASP.NET Core hosted services and REST API, MongoDB.Driver 3.9, System.ServiceModel.Syndication 10.0.9, HtmlSanitizer 9.0.892, React 19 TypeScript, Tailwind CSS, Axios, Vitest, Docker MongoDB.

**Test policy:** Backend and frontend tests are local ignored files. Run them for TDD and verification, but never stage or commit them.

---

## File Structure

### Backend persistence and transport

- `Models/NewsSource.cs`: RSS configuration and source health.
- `Models/EditorialAuditEvent.cs`: immutable news/source audit entry.
- `Models/Post.cs`: nullable imported-news metadata.
- `Configuration/NewsIngestionSettings.cs`: worker, fetch, and feature-flag limits.
- `Requests/NewsRequests.cs`: timeline, article, X, source, and pause requests.
- `Responses/NewsResponses.cs`: cursor page, article detail, and source-health contracts.
- `Data/MongoContext.cs`, `Configuration/MongoDbSettings.cs`, `Data/MongoIndexInitializer.cs`: collections and indexes.
- `Data/NewsMetadataMigration.cs`: idempotent backfill for existing news posts.

### Backend ingestion and API

- `Repositories/INewsRepository.cs`, `Repositories/MongoNewsRepository.cs`: timeline, source CRUD, duplicate checks, atomic promotion, and leases.
- `Services/News/INewsFeedProvider.cs`: normalized feed-provider contract.
- `Services/News/SyndicationNewsFeedProvider.cs`: structured RSS/Atom parsing.
- `Services/News/INewsFeedClient.cs`, `Services/News/SafeNewsFeedClient.cs`: SSRF-safe bounded HTTP access.
- `Services/News/NewsRelevanceFilter.cs`: deterministic PL/FPL filtering.
- `Services/News/NewsFingerprint.cs`: canonical URL and title duplicate keys.
- `Services/News/INewsIngestionService.cs`, `Services/News/NewsIngestionService.cs`: one-source ingestion transaction.
- `Services/News/NewsIngestionWorker.cs`: five-minute scheduling.
- `Services/INewsService.cs`, `Services/NewsService.cs`: timeline, detail, and editorial operations.
- `Services/ICommentService.cs`, `Services/CommentService.cs`: shared news/forum comments and voting.
- `Controllers/NewsController.cs`: public and editorial news routes.
- `Controllers/NewsSourcesController.cs`: source administration routes.
- `Program.cs`, `appsettings.json`, `appsettings.Development.json`: DI and ingestion feature flag.

### Frontend

- `types/api.ts`: news, source, cursor, editor, and filter types.
- `services/newsApi.ts`: complete news and source API client.
- `components/news/NewsBadge.tsx`: four reliability badges.
- `components/news/NewsTimeline.tsx`, `NewsTimelineItem.tsx`, `NewsFilters.tsx`: approved live timeline.
- `components/news/NewsEditor.tsx`: article and X modes.
- `components/news/XEmbed.tsx`: embed plus unavailable fallback.
- `pages/News.tsx`: timeline state and incremental loading.
- `pages/NewsDetail.tsx`: source content and shared comments.
- `pages/NewsSources.tsx`: operational source table.
- `App.tsx`: detail and protected source routes.

---

### Task 1: Persist News Metadata, Sources, Audit Events, and Indexes

**Files:**
- Modify: `backend/PLeagueHub.Api/PLeagueHub.Api.csproj`
- Modify: `backend/PLeagueHub.Api/Models/Post.cs`
- Create: `backend/PLeagueHub.Api/Models/NewsSource.cs`
- Create: `backend/PLeagueHub.Api/Models/EditorialAuditEvent.cs`
- Create: `backend/PLeagueHub.Api/Configuration/NewsIngestionSettings.cs`
- Modify: `backend/PLeagueHub.Api/Configuration/MongoDbSettings.cs`
- Modify: `backend/PLeagueHub.Api/Data/MongoContext.cs`
- Modify: `backend/PLeagueHub.Api/Data/MongoIndexInitializer.cs`
- Create: `backend/PLeagueHub.Api/Data/NewsMetadataMigration.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Modify: `backend/PLeagueHub.Api/appsettings.json`
- Test locally: `backend/PLeagueHub.Api.Tests/NewsInfrastructureTests.cs`

- [ ] **Step 1: Add failing BSON and index contract tests locally**

Assert exact BSON names for every new `Post` field, `NewsSource`, and `EditorialAuditEvent`; assert generic Mongo collection resolution; assert settings default to a disabled worker and five-minute interval.

```csharp
Assert.Equal("sourceId", BsonName<Post>(nameof(Post.SourceId)));
Assert.Equal("pouzdanost", BsonName<Post>(nameof(Post.Pouzdanost)));
Assert.Equal("poslednjiUspehAt", BsonName<NewsSource>(nameof(NewsSource.PoslednjiUspehAt)));
Assert.False(new NewsIngestionSettings().WorkerEnabled);
Assert.Equal(TimeSpan.FromMinutes(5), new NewsIngestionSettings().Interval);
```

- [ ] **Step 2: Run the local infrastructure test and verify failure**

Run:

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter NewsInfrastructureTests
```

Expected: compile failure because the new models and settings do not exist.

- [ ] **Step 3: Add stable parser and sanitizer packages**

Add exactly:

```xml
<PackageReference Include="System.ServiceModel.Syndication" Version="10.0.9" />
<PackageReference Include="HtmlSanitizer" Version="9.0.892" />
```

- [ ] **Step 4: Add persistence models and settings**

Extend `Post` with nullable metadata from the approved spec and change `AutorId` to nullable. Add these core models:

```csharp
public sealed class NewsSource : BaseDocument
{
    [BsonElement("naziv")] public string Naziv { get; set; } = string.Empty;
    [BsonElement("feedUrl")] public string FeedUrl { get; set; } = string.Empty;
    [BsonElement("siteUrl")] public string SiteUrl { get; set; } = string.Empty;
    [BsonElement("tip")] public string Tip { get; set; } = "rss";
    [BsonElement("podrazumevanaKategorija")] public string PodrazumevanaKategorija { get; set; } = "premier_league";
    [BsonElement("podrazumevanaPouzdanost")] public string PodrazumevanaPouzdanost { get; set; } = "pouzdan_izvor";
    [BsonElement("ukljuceniPojmovi")] public List<string> UkljuceniPojmovi { get; set; } = [];
    [BsonElement("iskljuceniPojmovi")] public List<string> IskljuceniPojmovi { get; set; } = [];
    [BsonElement("aktivan")] public bool Aktivan { get; set; } = true;
    [BsonElement("pauziranRazlog")] public string? PauziranRazlog { get; set; }
    [BsonElement("uzastopneGreske")] public int UzastopneGreske { get; set; }
    [BsonElement("etag")] public string? Etag { get; set; }
    [BsonElement("lastModified")] public DateTimeOffset? LastModified { get; set; }
    [BsonElement("poslednjaProveraAt")] public DateTime? PoslednjaProveraAt { get; set; }
    [BsonElement("poslednjiUspehAt")] public DateTime? PoslednjiUspehAt { get; set; }
    [BsonElement("createdBy")] public string CreatedBy { get; set; } = string.Empty;
    [BsonElement("updatedBy")] public string UpdatedBy { get; set; } = string.Empty;
    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
}

public sealed class NewsIngestionSettings
{
    public bool WorkerEnabled { get; init; }
    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);
    public int MaxResponseBytes { get; init; } = 2_000_000;
    public int MaxRedirects { get; init; } = 3;
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);
}
```

- [ ] **Step 5: Register collections and deterministic indexes**

Add `NewsSourcesCollectionName` and `EditorialAuditEventsCollectionName`, generic collection resolution, plus indexes:

```csharp
new CreateIndexModel<Post>(
    Builders<Post>.IndexKeys.Ascending(x => x.Tip).Descending(x => x.PublishedAt).Descending(x => x.Id)),
new CreateIndexModel<Post>(
    Builders<Post>.IndexKeys.Ascending(x => x.OriginalUrl),
    new CreateIndexOptions { Unique = true, Sparse = true }),
new CreateIndexModel<Post>(
    Builders<Post>.IndexKeys.Ascending(x => x.ExternalId),
    new CreateIndexOptions { Unique = true, Sparse = true }),
new CreateIndexModel<NewsSource>(
    Builders<NewsSource>.IndexKeys.Ascending(x => x.Aktivan).Ascending(x => x.PoslednjaProveraAt))
```

- [ ] **Step 6: Backfill existing news before serving requests**

Create an idempotent migration that updates `tip == "vest"` documents only when fields are missing:

```csharp
PublishedAt = DatumKreiranja,
FetchedAt = DatumKreiranja,
Kategorija = "premier_league",
Pouzdanost = "pouzdan_izvor",
UvozAutomatski = false,
UpdatedAt = DatumKreiranja
```

Register `NewsMetadataMigration` as a singleton and run it after Mongo indexes are available but before seed/startup request handling. Add a local test proving a second run changes zero documents.

- [ ] **Step 7: Run tests and build**

Run the Step 2 command and:

```powershell
dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj --no-restore
```

Expected: infrastructure tests pass; build has zero errors.

- [ ] **Step 8: Commit implementation only**

```powershell
git add backend/PLeagueHub.Api
git commit -m "Add news aggregation persistence"
```

### Task 2: Add News Repository, Cursor Paging, and Source CRUD

**Files:**
- Create: `backend/PLeagueHub.Api/Repositories/INewsRepository.cs`
- Create: `backend/PLeagueHub.Api/Repositories/MongoNewsRepository.cs`
- Create: `backend/PLeagueHub.Api/Services/News/NewsCursorCodec.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/NewsRepositoryTests.cs`

- [ ] **Step 1: Write failing repository tests locally**

Cover filter composition, ordering by `PublishedAt desc, Id desc`, opaque cursor round-trip, active due sources, source soft-deactivation, duplicate lookup priority, and atomic rumor promotion.

```csharp
var cursor = NewsCursorCodec.Encode(publishedAt, "665000000000000000000401");
Assert.Equal((publishedAt, "665000000000000000000401"), NewsCursorCodec.Decode(cursor));
```

- [ ] **Step 2: Run and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter NewsRepositoryTests
```

Expected: missing repository and cursor types.

- [ ] **Step 3: Implement repository contracts**

Define explicit query/result contracts and atomic methods:

```csharp
public sealed record NewsQuery(
    string? Category,
    string? Reliability,
    string? SourceId,
    DateTime? BeforePublishedAt,
    string? BeforeId,
    int Limit);

public interface INewsRepository
{
    Task<IReadOnlyList<Post>> GetTimelineAsync(NewsQuery query, CancellationToken ct = default);
    Task<Post?> GetVisibleAsync(string id, CancellationToken ct = default);
    Task<Post?> FindDuplicateAsync(string? externalId, string originalUrl, string fingerprint, CancellationToken ct = default);
    Task<Post> CreateAsync(Post post, CancellationToken ct = default);
    Task<bool> UpdateAsync(string id, Post post, CancellationToken ct = default);
    Task<bool> PromoteRumorAsync(string id, Post officialPost, CancellationToken ct = default);
    Task<IReadOnlyList<NewsSource>> GetDueSourcesAsync(DateTime dueBefore, CancellationToken ct = default);
    Task<IReadOnlyList<NewsSource>> GetSourcesAsync(CancellationToken ct = default);
    Task<NewsSource?> GetSourceAsync(string id, CancellationToken ct = default);
    Task<NewsSource> CreateSourceAsync(NewsSource source, CancellationToken ct = default);
    Task<bool> UpdateSourceAsync(string id, NewsSource source, CancellationToken ct = default);
    Task RecordAuditAsync(EditorialAuditEvent auditEvent, CancellationToken ct = default);
}
```

Cursor encoding serializes `{ publishedAt, id }` JSON with Base64Url; invalid cursors return a validation error instead of silently resetting the feed.

- [ ] **Step 4: Implement Mongo filters and DI**

Use typed Mongo builders. `GetTimelineAsync` filters `tip == "vest" && !obrisan`, applies optional filters, then applies:

```csharp
Builders<Post>.Filter.Lt(post => post.PublishedAt, beforePublishedAt)
| (Builders<Post>.Filter.Eq(post => post.PublishedAt, beforePublishedAt)
   & Builders<Post>.Filter.Lt(post => post.Id, beforeId))
```

Fetch `limit + 1` items so the service can determine whether another cursor exists. Register `INewsRepository` as scoped.

- [ ] **Step 5: Run focused tests and commit implementation**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter NewsRepositoryTests
git add backend/PLeagueHub.Api
git commit -m "Add news repository and cursor paging"
```

### Task 3: Parse and Sanitize RSS and Atom Feeds

**Files:**
- Create: `backend/PLeagueHub.Api/Services/News/INewsFeedProvider.cs`
- Create: `backend/PLeagueHub.Api/Services/News/SyndicationNewsFeedProvider.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/SyndicationNewsFeedProviderTests.cs`
- Test fixtures locally: `backend/PLeagueHub.Api.Tests/Fixtures/rss.xml`, `atom.xml`, `malformed.xml`

- [ ] **Step 1: Write parser tests with deterministic local XML fixtures**

Assert RSS and Atom map title, author, canonical link, external ID, publication timestamp, excerpt, and image; unsafe script/event attributes never survive.

```csharp
var entries = await provider.ParseAsync(stream, source, CancellationToken.None);
Assert.Equal("Arsenal open talks", entries[0].Title);
Assert.DoesNotContain("script", entries[0].Excerpt, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Run and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter SyndicationNewsFeedProviderTests
```

Expected: provider types do not exist.

- [ ] **Step 3: Define normalized provider output**

```csharp
public sealed record NormalizedNewsEntry(
    string? ExternalId,
    string Title,
    string Excerpt,
    string? Author,
    string OriginalUrl,
    string? ImageUrl,
    DateTime PublishedAt);

public interface INewsFeedProvider
{
    Task<IReadOnlyList<NormalizedNewsEntry>> ParseAsync(
        Stream feed,
        NewsSource source,
        CancellationToken ct = default);
}
```

- [ ] **Step 4: Implement structured parsing and text sanitization**

Use `SyndicationFeed.Load(XmlReader)` with DTD prohibited and external resolution disabled. Use `HtmlSanitizer` with no allowed tags/attributes for excerpts, then HTML-decode and trim to 500 characters. Reject entries without a title, HTTPS canonical URL, or valid publication timestamp.

- [ ] **Step 5: Run tests and commit implementation**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter SyndicationNewsFeedProviderTests
git add backend/PLeagueHub.Api
git commit -m "Parse and sanitize news feeds"
```

### Task 4: Build the SSRF-Safe Bounded Feed Client

**Files:**
- Create: `backend/PLeagueHub.Api/Services/News/INewsFeedClient.cs`
- Create: `backend/PLeagueHub.Api/Services/News/SafeNewsFeedClient.cs`
- Create: `backend/PLeagueHub.Api/Services/News/PublicAddressValidator.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/SafeNewsFeedClientTests.cs`

- [ ] **Step 1: Write failing security tests locally**

Cover `http`, localhost, IPv4/IPv6 loopback, RFC1918, link-local, multicast, reserved ranges, credentials in URLs, DNS resolving to private IP, redirects to private IP, more than three redirects, oversized bodies, wrong content types, timeout, cancellation, and valid public HTTPS.

```csharp
[Theory]
[InlineData("http://example.com/feed.xml")]
[InlineData("https://127.0.0.1/feed.xml")]
[InlineData("https://[::1]/feed.xml")]
public async Task FetchAsync_RejectsUnsafeUrls(string url) =>
    Assert.Equal(NewsFetchError.UnsafeAddress, (await client.FetchAsync(url, null, null)).Error);
```

- [ ] **Step 2: Run and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter SafeNewsFeedClientTests
```

Expected: missing safe-client types.

- [ ] **Step 3: Implement public address validation**

Require HTTPS, no user-info, and port 443. Resolve every hostname before every request and redirect. Reject any resolved address in loopback, private, carrier-grade NAT, link-local, multicast, documentation, benchmark, unspecified, and reserved ranges for both IPv4 and IPv6.

Use `SocketsHttpHandler.ConnectCallback` to connect directly to one validated resolved public address while preserving the original hostname for TLS SNI and certificate validation. Do not perform validation followed by a normal hostname connection, because that leaves a DNS-rebinding gap.

- [ ] **Step 4: Implement bounded fetch behavior**

Return a discriminated result:

```csharp
public sealed record NewsFeedFetchResult(
    Stream? Content,
    HttpStatusCode? StatusCode,
    string? Etag,
    DateTimeOffset? LastModified,
    NewsFetchError Error,
    string? Message);
```

Disable automatic redirects; validate and follow at most `MaxRedirects`. Send `If-None-Match`/`If-Modified-Since`. Accept RSS/Atom XML content types, stream through a counting wrapper, and abort above `MaxResponseBytes`. Use the configured 15-second linked cancellation timeout.

- [ ] **Step 5: Run security tests and commit implementation**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter SafeNewsFeedClientTests
git add backend/PLeagueHub.Api
git commit -m "Add safe bounded news feed fetching"
```

### Task 5: Filter, Fingerprint, Deduplicate, and Ingest News

**Files:**
- Create: `backend/PLeagueHub.Api/Services/News/NewsRelevanceFilter.cs`
- Create: `backend/PLeagueHub.Api/Services/News/NewsFingerprint.cs`
- Create: `backend/PLeagueHub.Api/Services/News/INewsIngestionService.cs`
- Create: `backend/PLeagueHub.Api/Services/News/NewsIngestionService.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/NewsIngestionServiceTests.cs`

- [ ] **Step 1: Write failing filtering and ingestion tests locally**

Cover all 20 current PL teams and aliases, Premier League terms, PL-linked transfer phrases, FPL terms, include/exclude overrides, irrelevant European stories, duplicate external ID/URL/fingerprint, first-source retention, official upgrade of rumor, unique-index race, `304`, health reset, and three-error pause.

```csharp
Assert.True(filter.IsRelevant("Arsenal open talks for new midfielder", source));
Assert.False(filter.IsRelevant("La Liga title race continues", source));
Assert.Equal(IngestionOutcome.PromotedToOfficial, result.Entries[0].Outcome);
```

- [ ] **Step 2: Run and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter NewsIngestionServiceTests
```

Expected: ingestion services do not exist.

- [ ] **Step 3: Implement deterministic relevance and fingerprints**

Normalize Unicode, case, punctuation, whitespace, tracking query parameters, and common mobile URL variants. A story is relevant when an include override matches, an FPL term matches, or a PL league/team alias matches; any exclude term wins. Compute SHA-256 over the normalized canonical URL and normalized title token sequence.

- [ ] **Step 4: Implement one-source ingestion**

Expose:

```csharp
public interface INewsIngestionService
{
    Task<NewsSourceSyncResponse> SyncSourceAsync(
        string sourceId,
        string? actorId,
        CancellationToken ct = default);
}
```

Fetch, parse, filter, and process entries oldest-to-newest so timeline timestamps remain source timestamps. Keep the first duplicate. Promote only when existing `Pouzdanost == "glasina"` and source default is `zvanicno`. Catch Mongo duplicate-key errors and report `duplicate`, not `failed`. On failure increment source errors atomically; at three errors set `Aktivan = false` and Serbian pause reason.

- [ ] **Step 5: Run tests and commit implementation**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter NewsIngestionServiceTests
git add backend/PLeagueHub.Api
git commit -m "Ingest and deduplicate relevant news"
```

### Task 6: Schedule Ingestion and Seed Disabled Free Sources

**Files:**
- Create: `backend/PLeagueHub.Api/Services/News/NewsIngestionWorker.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Modify: `backend/PLeagueHub.Api/Data/Seeding/DatabaseSeeder.cs`
- Modify: `backend/PLeagueHub.Api/appsettings.json`
- Test locally: `backend/PLeagueHub.Api.Tests/NewsIngestionWorkerTests.cs`

- [ ] **Step 1: Write failing worker tests locally**

Use fake `TimeProvider` and ingestion service. Assert the worker does nothing when disabled, runs due sources every five minutes when enabled, isolates per-source failures, respects cancellation, and never overlaps its own cycle.

- [ ] **Step 2: Run and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter NewsIngestionWorkerTests
```

Expected: worker does not exist.

- [ ] **Step 3: Implement worker and DI**

Use `PeriodicTimer(settings.Interval, timeProvider)` and a scoped service per cycle. Register with `AddHostedService<NewsIngestionWorker>()`; retain `NewsIngestion:WorkerEnabled=false` by default.

- [ ] **Step 4: Seed three validated sources as inactive**

Seed these exact HTTPS feeds with stable IDs and `Aktivan=false` so administrators deliberately enable them:

```text
BBC Football: https://feeds.bbci.co.uk/sport/football/rss.xml
Sky Sports Football: https://www.skysports.com/rss/12040
Fantasy Football Scout: https://www.fantasyfootballscout.co.uk/feed/
```

BBC and Sky default to `pouzdan_izvor`; Fantasy Football Scout defaults to `fpl_analiza` and category `fpl`.

- [ ] **Step 5: Run tests and commit implementation**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter NewsIngestionWorkerTests
git add backend/PLeagueHub.Api
git commit -m "Schedule configurable news ingestion"
```

### Task 7: Extract Shared Comment and Vote Behavior

**Files:**
- Create: `backend/PLeagueHub.Api/Services/ICommentService.cs`
- Create: `backend/PLeagueHub.Api/Services/CommentService.cs`
- Modify: `backend/PLeagueHub.Api/Services/ForumService.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/CommentServiceTests.cs`
- Test locally: existing `ForumServiceTests.cs`

- [ ] **Step 1: Write local characterization and news-target tests**

Preserve current DFS numbering, nesting, deletion placeholders, votes, self-vote rejection, mute enforcement, and role metadata. Add a target validator proving comments work for visible `tip=vest` posts but reject deleted/missing posts.

- [ ] **Step 2: Run characterization tests**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter "ForumServiceTests|CommentServiceTests"
```

Expected: existing forum tests pass; new comment-service tests fail to compile.

- [ ] **Step 3: Extract the shared service without changing responses**

Define:

```csharp
public interface ICommentService
{
    Task<IReadOnlyList<ForumCommentResponse>?> GetCommentsAsync(string postId, string? userId, CancellationToken ct = default);
    Task<ForumResult<ForumCommentResponse>> CreateAsync(string postId, CreateCommentRequest request, string? userId, CancellationToken ct = default);
    Task<ForumResult<ForumVoteResponse>> VoteAsync(string commentId, string? userId, int value, CancellationToken ct = default);
    Task<ForumResult<ForumVoteResponse>> RemoveVoteAsync(string commentId, string? userId, CancellationToken ct = default);
}
```

Move existing comment/vote logic intact. Inject a post-visibility check accepting non-deleted `diskusija` or `vest`. Make `ForumService` delegate its four comment methods to `ICommentService`.

- [ ] **Step 4: Run tests and commit implementation**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter "ForumServiceTests|CommentServiceTests"
git add backend/PLeagueHub.Api
git commit -m "Share moderated comments across posts"
```

### Task 8: Expose Timeline, Details, Editorial Actions, and Source API

**Files:**
- Create: `backend/PLeagueHub.Api/Requests/NewsRequests.cs`
- Create: `backend/PLeagueHub.Api/Responses/NewsResponses.cs`
- Create: `backend/PLeagueHub.Api/Services/INewsService.cs`
- Create: `backend/PLeagueHub.Api/Services/NewsService.cs`
- Replace: `backend/PLeagueHub.Api/Controllers/NewsController.cs`
- Create: `backend/PLeagueHub.Api/Controllers/NewsSourcesController.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/NewsEndpointsTests.cs`

- [ ] **Step 1: Write failing endpoint tests locally**

Cover public cursor filters/detail/comments; registered comment; moderator/admin create article and X item; registered editorial `403`; URL/category/reliability validation; edit audit; soft delete; source list/create/edit/deactivate/pause/resume/sync; automatic-post editing; and Serbian API errors.

- [ ] **Step 2: Run and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter NewsEndpointsTests
```

Expected: new routes return `404` or contracts fail to compile.

- [ ] **Step 3: Add validated contracts**

Use allowlists:

```csharp
private static readonly string[] Categories = ["premier_league", "transferi", "fpl", "klubovi"];
private static readonly string[] Reliability = ["zvanicno", "pouzdan_izvor", "glasina", "fpl_analiza"];
```

`CreateXNewsRequest` requires a regex-equivalent parsed URI whose host is `x.com` or `www.x.com` and path has exactly `{username}/status/{numericId}`. Do not accept arbitrary embed HTML.

- [ ] **Step 4: Implement service and controllers**

Return `NewsTimelineResponse` with `items` and nullable `nextCursor`. Resolve manual author usernames in batches. Map source health and use `publishedAt` from the source, falling back to `fetchedAt` only when missing during manual creation. Add `[Authorize(Roles = "moderator,administrator")]` to editorial/source mutations. Route news comments through `ICommentService` and map `ForumError` exactly like `ForumController`.

- [ ] **Step 5: Run endpoint tests, inspect Swagger, and commit**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter NewsEndpointsTests
dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj --no-restore
git add backend/PLeagueHub.Api
git commit -m "Expose live news and source management APIs"
```

Expected Swagger includes all public, editorial, comment, and source routes from the design.

### Task 9: Build Frontend Contracts, API Client, Badges, and Timeline

**Files:**
- Modify: `frontend/src/types/api.ts`
- Replace: `frontend/src/services/newsApi.ts`
- Create: `frontend/src/components/news/NewsBadge.tsx`
- Create: `frontend/src/components/news/NewsFilters.tsx`
- Create: `frontend/src/components/news/NewsTimelineItem.tsx`
- Create: `frontend/src/components/news/NewsTimeline.tsx`
- Replace: `frontend/src/pages/News.tsx`
- Test locally: `frontend/src/services/newsApi.test.ts`
- Test locally: `frontend/src/pages/News.test.tsx`

- [ ] **Step 1: Write failing frontend tests locally**

Assert exact API routes/query params, four Serbian badge labels, chronological timeline, category filtering, cursor append without duplicates, role-only `Objavi vest`, retry, empty state, and exact/relative source times.

- [ ] **Step 2: Run and verify failure**

```powershell
Set-Location frontend
npm.cmd test -- --run src/services/newsApi.test.ts src/pages/News.test.tsx
```

Expected: missing timeline types/components and old array API mismatch.

- [ ] **Step 3: Add strict TypeScript contracts and client methods**

Define literal unions and cursor response:

```ts
export type NewsCategory = 'premier_league' | 'transferi' | 'fpl' | 'klubovi';
export type NewsReliability = 'zvanicno' | 'pouzdan_izvor' | 'glasina' | 'fpl_analiza';
export interface NewsTimelineResponse { items: NewsItem[]; nextCursor: string | null; }
```

Implement list/detail/create/edit/remove/X/source/comment methods against the approved endpoints.

- [ ] **Step 4: Implement the approved live timeline**

Use the current restrained Flashscore-like palette. Filters are familiar segmented controls, each row is a real link to `/news/{id}`, badge colors remain distinct and accessible, and `Ucitaj jos` appends the next cursor. Do not use decorative cards around the whole page.

- [ ] **Step 5: Run focused tests, build, and commit implementation**

```powershell
npm.cmd test -- --run src/services/newsApi.test.ts src/pages/News.test.tsx
npm.cmd run build
Set-Location ..
git add frontend/src
git commit -m "Build live news timeline"
```

### Task 10: Add News Details, X Fallback, and Shared Comments

**Files:**
- Create: `frontend/src/components/news/XEmbed.tsx`
- Create: `frontend/src/pages/NewsDetail.tsx`
- Modify: `frontend/src/App.tsx`
- Reuse: `frontend/src/components/forum/ForumThread.tsx`
- Test locally: `frontend/src/pages/NewsDetail.test.tsx`

- [ ] **Step 1: Write failing detail tests locally**

Assert detail metadata, original-language body/excerpt, original link, X embed initialization, unavailable fallback, threaded comments, login redirect on reply/vote, moderator controls, and no horizontal overflow at 390 px.

- [ ] **Step 2: Run and verify failure**

```powershell
Set-Location frontend
npm.cmd test -- --run src/pages/NewsDetail.test.tsx
```

Expected: route and page do not exist.

- [ ] **Step 3: Implement safe X rendering and detail page**

`XEmbed` accepts only the validated URL returned by the API, loads `https://platform.twitter.com/widgets.js` once, calls `twttr.widgets.createTweet(statusId, container)`, and shows a bounded fallback after load failure. It never accepts raw HTML. The detail page reuses `ForumThread`, vote behavior, moderation modal, and reply form from the forum discussion page.

- [ ] **Step 4: Add route and run tests/build**

```tsx
<Route path="news/:id" element={<NewsDetail />} />
```

Run:

```powershell
npm.cmd test -- --run src/pages/NewsDetail.test.tsx src/pages/ForumDiscussion.test.tsx
npm.cmd run build
```

- [ ] **Step 5: Commit implementation**

```powershell
Set-Location ..
git add frontend/src
git commit -m "Add news details and comments"
```

### Task 11: Add Manual Editor and Source Management

**Files:**
- Create: `frontend/src/components/news/NewsEditor.tsx`
- Create: `frontend/src/pages/NewsSources.tsx`
- Modify: `frontend/src/pages/News.tsx`
- Modify: `frontend/src/App.tsx`
- Test locally: `frontend/src/components/news/NewsEditor.test.tsx`
- Test locally: `frontend/src/pages/NewsSources.test.tsx`

- [ ] **Step 1: Write failing editor and source tests locally**

Cover article/X mode switch, required fields, X URL validation, immediate publish, API error retention, role visibility, source add/edit/deactivate, pause/resume, manual sync result, health display, and mobile action-menu accessibility.

- [ ] **Step 2: Run and verify failure**

```powershell
Set-Location frontend
npm.cmd test -- --run src/components/news/NewsEditor.test.tsx src/pages/NewsSources.test.tsx
```

Expected: editor and source page do not exist.

- [ ] **Step 3: Implement editor and source table**

Use segmented `Clanak | X objava`, category/reliability selects, title/body fields, URL field, pending/error state, and immediate list refresh. Source management uses a dense table with status, reliability, category, last success, error count, pause reason, sync, edit, pause/resume, and deactivate actions.

- [ ] **Step 4: Protect the source route and expose controls**

Add `/news/sources` behind:

```tsx
<ProtectedRoute allowedRoles={['moderator', 'administrator']} />
```

Show `Objavi vest` and `Izvori` only to the same roles.

- [ ] **Step 5: Run tests/build and commit implementation**

```powershell
npm.cmd test -- --run src/components/news/NewsEditor.test.tsx src/pages/NewsSources.test.tsx
npm.cmd run build
Set-Location ..
git add frontend/src
git commit -m "Add news publishing and source controls"
```

### Task 12: Integrated Docker, Swagger, Security, and Responsive Verification

**Files:**
- Verify all changed implementation files
- Keep local tests ignored and uncommitted

- [ ] **Step 1: Run the complete local backend suite**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --no-restore
```

Expected: zero failed tests.

- [ ] **Step 2: Run the complete local frontend suite and production build**

```powershell
Set-Location frontend
npm.cmd test -- --run
npm.cmd run build
Set-Location ..
```

Expected: zero failed tests and successful Vite build.

- [ ] **Step 3: Verify real MongoDB and Swagger flows**

With Docker MongoDB and API running, verify source create/edit/pause/resume/deactivate, manual sync of one fixture feed, idempotent repeated sync, rumor promotion, moderator/manual article, manual X item, registered comment/vote, mute blocking writes, source error auto-pause, and all documented Swagger routes.

- [ ] **Step 4: Verify safe fetching against a local fixture server**

Run the deterministic fake-DNS/fake-socket client suite for a valid public address and all unsafe cases. Against the running API, confirm loopback and private source URLs are rejected and create no posts; production internet feeds are not contacted during verification.

- [ ] **Step 5: Verify desktop and mobile UI with Playwright**

At 1440 x 1000 and 390 x 844 inspect timeline filters, all badges, incremental loading, editor, source table, RSS detail, X fallback, nested comments, moderator actions, loading/empty/error states, and assert:

```js
document.documentElement.scrollWidth === window.innerWidth
```

- [ ] **Step 6: Inspect Git state and push only implementation/spec commits**

```powershell
git diff --check
git status --short
git check-ignore backend/PLeagueHub.Api.Tests/NewsEndpointsTests.cs
git check-ignore frontend/src/pages/News.test.tsx
git log --oneline origin/master..HEAD
git push origin master
```

Expected: clean tracked worktree, local tests ignored, and successful push.
