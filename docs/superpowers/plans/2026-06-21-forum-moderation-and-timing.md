# Forum Moderation and Accurate Timing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add compact unlimited forum replies, role-aware moderation with timed mute/suspension, topic/comment pinning, truthful relative timestamps, and Git exclusions for local tests.

**Architecture:** Store one active moderation state on each user for fast authorization and append immutable events to a dedicated MongoDB audit collection. Keep moderation policy in a service used by authentication, forum mutations, and moderation endpoints; keep tree numbering and pin ordering deterministic before the React UI renders it.

**Tech Stack:** .NET 10, ASP.NET Core REST/Swagger, MongoDB.Driver, React 19 with TypeScript, Tailwind CSS, Axios, Vitest.

---

## File Structure

New backend files isolate moderation state, persistence, policy, and transport:

- `backend/PLeagueHub.Api/Models/ActiveModeration.cs`: embedded current restriction.
- `backend/PLeagueHub.Api/Models/ModerationAction.cs`: immutable audit document.
- `backend/PLeagueHub.Api/Repositories/IModerationRepository.cs`: atomic moderation operations.
- `backend/PLeagueHub.Api/Repositories/MongoModerationRepository.cs`: MongoDB implementation.
- `backend/PLeagueHub.Api/Services/IModerationService.cs`: result contracts and policy API.
- `backend/PLeagueHub.Api/Services/ModerationService.cs`: hierarchy, expiry, apply/revoke, write access.
- `backend/PLeagueHub.Api/Middleware/ActiveSuspensionMiddleware.cs`: rejects requests made with a suspended account, including an already issued JWT.
- `backend/PLeagueHub.Api/Requests/CreateModerationActionRequest.cs`: action request validation input.
- `backend/PLeagueHub.Api/Responses/ModerationStateResponse.cs`: user-visible state and expiry.

New frontend files keep the forum page components focused:

- `frontend/src/components/RelativeTime.tsx`: self-refreshing relative time.
- `frontend/src/components/forum/ModerationModal.tsx`: approved punishment dialog.
- `frontend/src/components/forum/CommentActionsMenu.tsx`: compact pin/delete menu.
- `frontend/src/components/forum/ModerationNotice.tsx`: mute/suspension reason and expiry.

Existing local test projects remain on disk but are removed from Git tracking.

### Task 1: Ignore Test and Brainstorm Artifacts

**Files:**
- Modify: `.gitignore`
- Untrack, preserve locally: `backend/PLeagueHub.Api.Tests/`
- Untrack, preserve locally: `frontend/src/test/`
- Untrack, preserve locally: `frontend/src/**/*.test.ts`
- Untrack, preserve locally: `frontend/src/**/*.test.tsx`

- [ ] **Step 1: Add explicit ignore rules**

Append:

```gitignore
# Local tests (kept on disk, not committed)
backend/PLeagueHub.Api.Tests/
frontend/src/test/
frontend/src/**/*.test.ts
frontend/src/**/*.test.tsx

# Superpowers visual brainstorming artifacts
.superpowers/
```

- [ ] **Step 2: Remove already tracked tests from the index only**

Run:

```powershell
git rm -r --cached -- backend/PLeagueHub.Api.Tests frontend/src/test
git rm --cached -- 'frontend/src/**/*.test.ts' 'frontend/src/**/*.test.tsx'
```

Expected: test files are staged as deleted while the same files still exist locally.

- [ ] **Step 3: Verify ignore behavior and local preservation**

Run:

```powershell
Test-Path backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj
git check-ignore frontend/src/pages/Forum.test.tsx
git status --short
```

Expected: `True`, the frontend test path is printed, and test deletions plus `.gitignore` are staged/visible.

- [ ] **Step 4: Commit**

```powershell
git add .gitignore
git commit -m "Stop tracking local test files"
```

### Task 2: Add Moderation, Pin, and Match-Time Persistence

**Files:**
- Create: `backend/PLeagueHub.Api/Models/ActiveModeration.cs`
- Create: `backend/PLeagueHub.Api/Models/ModerationAction.cs`
- Modify: `backend/PLeagueHub.Api/Models/User.cs`
- Modify: `backend/PLeagueHub.Api/Models/Comment.cs`
- Modify: `backend/PLeagueHub.Api/Models/Match.cs`
- Modify: `backend/PLeagueHub.Api/Configuration/MongoDbSettings.cs`
- Modify: `backend/PLeagueHub.Api/Data/MongoContext.cs`
- Modify: `backend/PLeagueHub.Api/Data/MongoIndexInitializer.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/ForumInfrastructureTests.cs`

- [ ] **Step 1: Write failing BSON and context tests**

Add assertions that `User.AktivnaModeracija` maps to `aktivnaModeracija`, comment pin fields map to `istaknut`, `istaknutAt`, `istakaoId`, match completion maps to `zavrsenaAt`, and `MongoContext.GetCollection<ModerationAction>()` resolves the configured collection.

```csharp
Assert.Equal("aktivnaModeracija", GetBsonElementName(typeof(User), nameof(User.AktivnaModeracija)));
Assert.Equal("istaknut", GetBsonElementName(typeof(Comment), nameof(Comment.Istaknut)));
Assert.Equal("zavrsenaAt", GetBsonElementName(typeof(Match), nameof(Match.ZavrsenaAt)));
```

- [ ] **Step 2: Run the focused test and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter ForumInfrastructureTests --artifacts-path "$env:TEMP\pleaguehub-artifacts"
```

Expected: compile failure because the new properties/types do not exist.

- [ ] **Step 3: Add the persistence models**

Implement these contracts:

```csharp
public sealed class ActiveModeration
{
    [BsonElement("tip")] public string Tip { get; set; } = string.Empty;
    [BsonElement("razlog")] public string Razlog { get; set; } = string.Empty;
    [BsonElement("pocetak")] public DateTime Pocetak { get; set; }
    [BsonElement("isticeAt")] public DateTime? IsticeAt { get; set; }
    [BsonElement("moderatorId")] public string ModeratorId { get; set; } = string.Empty;
}

public sealed class ModerationAction : BaseDocument
{
    [BsonElement("korisnikId")] public string KorisnikId { get; set; } = string.Empty;
    [BsonElement("moderatorId")] public string ModeratorId { get; set; } = string.Empty;
    [BsonElement("akcija")] public string Akcija { get; set; } = string.Empty;
    [BsonElement("tipMere")] public string? TipMere { get; set; }
    [BsonElement("razlog")] public string? Razlog { get; set; }
    [BsonElement("pocetak")] public DateTime? Pocetak { get; set; }
    [BsonElement("isticeAt")] public DateTime? IsticeAt { get; set; }
    [BsonElement("datum")] public DateTime Datum { get; set; } = DateTime.UtcNow;
}
```

Add nullable `AktivnaModeracija` to `User`; pin metadata to `Comment`; nullable `ZavrsenaAt` to `Match`; and configure `ModerationActionsCollectionName`, collection access, generic resolution, and indexes on `(korisnikId, datum)` plus `(postId, istaknut, istaknutAt)`.

- [ ] **Step 4: Run the focused test**

Run the command from Step 2. Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add backend/PLeagueHub.Api
git commit -m "Add moderation and pin persistence"
```

### Task 3: Implement Moderation Policy and Atomic Repository

**Files:**
- Create: `backend/PLeagueHub.Api/Repositories/IModerationRepository.cs`
- Create: `backend/PLeagueHub.Api/Repositories/MongoModerationRepository.cs`
- Create: `backend/PLeagueHub.Api/Services/IModerationService.cs`
- Create: `backend/PLeagueHub.Api/Services/ModerationService.cs`
- Create: `backend/PLeagueHub.Api/Requests/CreateModerationActionRequest.cs`
- Create: `backend/PLeagueHub.Api/Responses/ModerationStateResponse.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/ModerationServiceTests.cs`

- [ ] **Step 1: Write failing hierarchy, duration, and expiry tests**

Cover these exact cases: moderator can mute a registered user; moderator cannot act on moderator/admin; admin can suspend moderator; admin cannot act on admin; blank reason fails; `1h`, `24h`, `7d`, `30d`, `permanent` map to exact expiry; expired state clears once and writes one `istekla` audit event.

```csharp
var result = await service.ApplyAsync(
    registeredUser.Id!,
    admin.Id!,
    new CreateModerationActionRequest("mute", "24h", "Spam"));
Assert.Equal(now.AddHours(24), result.Value!.IsticeAt);
```

- [ ] **Step 2: Run focused tests and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter ModerationServiceTests --artifacts-path "$env:TEMP\pleaguehub-artifacts"
```

Expected: compile failure for missing moderation service.

- [ ] **Step 3: Implement service contracts and result errors**

Expose:

```csharp
Task<ModerationResult<ModerationStateResponse>> ApplyAsync(string targetId, string actorId, CreateModerationActionRequest request, CancellationToken ct = default);
Task<ModerationResult<ModerationStateResponse>> RevokeAsync(string targetId, string actorId, CancellationToken ct = default);
Task<ModerationAccessResult> CheckLoginAsync(User user, CancellationToken ct = default);
Task<ModerationAccessResult> CheckForumWriteAsync(string userId, CancellationToken ct = default);
Task<bool> CanModerateContentAsync(string actorId, string authorId, CancellationToken ct = default);
```

Use role ranks `registrovani = 1`, `moderator = 2`, `administrator = 3`, with the rule `actorRank > targetRank` and no administrator target. Parse only the five approved durations. Repository updates must filter by target ID and expected moderation start timestamp when expiring, so two concurrent checks cannot create duplicate expiry events.

- [ ] **Step 4: Register repository and service with scoped DI**

```csharp
builder.Services.AddScoped<IModerationRepository, MongoModerationRepository>();
builder.Services.AddScoped<IModerationService, ModerationService>();
```

- [ ] **Step 5: Run focused tests**

Run Step 2. Expected: all `ModerationServiceTests` pass.

- [ ] **Step 6: Commit**

```powershell
git add backend/PLeagueHub.Api
git commit -m "Add role-aware moderation policy"
```

### Task 4: Expose Moderation API and Enforce Restrictions

**Files:**
- Modify: `backend/PLeagueHub.Api/Controllers/ModerationController.cs`
- Modify: `backend/PLeagueHub.Api/Controllers/ForumController.cs`
- Modify: `backend/PLeagueHub.Api/Services/ForumService.cs`
- Modify: `backend/PLeagueHub.Api/Services/AuthService.cs`
- Modify: `backend/PLeagueHub.Api/Responses/AuthResponse.cs`
- Modify: `backend/PLeagueHub.Api/Responses/UserProfileResponse.cs`
- Modify: `backend/PLeagueHub.Api/Controllers/UsersController.cs`
- Create: `backend/PLeagueHub.Api/Middleware/ActiveSuspensionMiddleware.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/ModerationEndpointsTests.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/ForumServiceTests.cs`

- [ ] **Step 1: Write failing endpoint and enforcement tests**

Verify `POST users/{id}/actions`, `DELETE users/{id}/action`, comment deletion, all four pin routes, `403` hierarchy failures, suspended login containing reason/expiry, an already issued JWT rejected on `/api/users/me`, and muted create-comment/vote failures containing reason/expiry.

```csharp
var response = await client.PostAsJsonAsync(
    $"/api/moderation/users/{userId}/actions",
    new { tip = "mute", trajanje = "7d", razlog = "Vredjanje" });
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```

- [ ] **Step 2: Run focused tests and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter "ModerationEndpointsTests|ForumServiceTests" --artifacts-path "$env:TEMP\pleaguehub-artifacts"
```

Expected: new endpoint assertions fail.

- [ ] **Step 3: Replace the old one-way suspend endpoint**

Inject `IModerationService`, derive actor ID from `ClaimTypes.NameIdentifier`, map service errors to `400/401/403/404`, and expose the routes from the approved spec. Keep soft deletion and make repeat pin/unpin return `204`.

- [ ] **Step 4: Enforce login and forum-write restrictions**

Inject `IModerationService` into `AuthService` and `ForumService`. After password verification, reject active suspension with a response body containing `message`, `tip`, and `isticeAt`. Before discussion creation, comment creation, vote, or vote removal, call `CheckForumWriteAsync`; map active mute to `ForumError.Forbidden`.

Add `ActiveSuspensionMiddleware` after `UseAuthentication` and before `UseAuthorization`. For every authenticated request, resolve the user ID and call `CheckLoginByUserIdAsync`; return `401` with the same structured restriction body when suspension is active. This closes the already-issued-JWT path instead of relying only on login-time checks.

- [ ] **Step 5: Expose active moderation in profile responses**

Add nullable `ModerationStateResponse AktivnaModeracija` to the profile contract so a muted user sees reason and remaining duration without another endpoint.

- [ ] **Step 6: Run focused tests and inspect Swagger**

Run Step 2, then start the API and verify all seven moderation paths exist in `/swagger/v1/swagger.json`.

- [ ] **Step 7: Commit**

```powershell
git add backend/PLeagueHub.Api
git commit -m "Expose and enforce forum moderation"
```

### Task 5: Add Deterministic Numbering and Pin Ordering

**Files:**
- Modify: `backend/PLeagueHub.Api/Repositories/IForumRepository.cs`
- Modify: `backend/PLeagueHub.Api/Repositories/MongoForumRepository.cs`
- Modify: `backend/PLeagueHub.Api/Services/ForumService.cs`
- Modify: `backend/PLeagueHub.Api/Responses/ForumCommentResponse.cs`
- Modify: `backend/PLeagueHub.Api/Responses/ForumTopicResponse.cs`
- Test locally: `backend/PLeagueHub.Api.Tests/ForumServiceTests.cs`

- [ ] **Step 1: Write failing tree-order tests**

Create comments in chronological order `root A`, `root B`, `reply to A`; expect response numbers `A = 1`, `reply = 2`, `B = 3`. Add a pinned nested comment and assert it retains `2`, includes pin metadata, and is not duplicated.

- [ ] **Step 2: Run and verify failure**

Use the Task 4 focused test command. Expected: reply currently keeps chronological number `3`.

- [ ] **Step 3: Implement cycle-safe depth-first numbering**

Build children by `ParentCommentId`, sort siblings by `DatumKreiranja` then ID, traverse roots depth-first, and append orphan/cyclic nodes once. Assign numbers from that traversal before any pinned presentation ordering. Extend comment responses with `Istaknut`, `IstaknutAt`, and `IstakaoId`; extend topic responses with `AutorUloga` so moderators do not receive controls for protected authors.

- [ ] **Step 4: Keep repository topic sorting deterministic**

Sort topics by `Istaknut desc`, `PoslednjaAktivnost desc`, then ID. Ensure pin updates do not rewrite creation time.

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter ForumServiceTests --artifacts-path "$env:TEMP\pleaguehub-artifacts"
git add backend/PLeagueHub.Api
git commit -m "Order pinned forum content deterministically"
```

### Task 6: Add Frontend Moderation Contracts and Modal

**Files:**
- Modify: `frontend/src/types/api.ts`
- Modify: `frontend/src/services/moderationApi.ts`
- Modify: `frontend/src/contexts/AuthContext.tsx`
- Create: `frontend/src/components/forum/ModerationModal.tsx`
- Create: `frontend/src/components/forum/ModerationNotice.tsx`
- Modify: `frontend/src/pages/Login.tsx`
- Test locally: `frontend/src/services/moderationApi.test.ts`
- Test locally: `frontend/src/components/forum/ModerationModal.test.tsx`

- [ ] **Step 1: Write failing API and modal tests**

Assert exact moderation routes/payloads, required reason, approved duration buttons, active-measure revoke, and Serbian login error rendering.

- [ ] **Step 2: Run and verify failure**

```powershell
Set-Location frontend
npm.cmd test -- --run src/services/moderationApi.test.ts src/components/forum/ModerationModal.test.tsx
```

Expected: missing module/types failures.

- [ ] **Step 3: Add TypeScript contracts and API methods**

Define `ModerationType = 'mute' | 'suspenzija'`, `ModerationDuration = '1h' | '24h' | '7d' | '30d' | 'permanent'`, `ModerationState`, and methods `applyUserAction`, `revokeUserAction`, `removeComment`, `pinPost`, `unpinPost`, `pinComment`, `unpinComment`.

- [ ] **Step 4: Build the approved modal**

Implement controlled type/duration selection, required reason, target identity, role warning, current action, revoke button, pending/error states, and `onChanged` callback. Do not close on failed requests.

- [ ] **Step 5: Surface restriction messages**

Add `aktivnaModeracija` to `UserProfile`, render `ModerationNotice` from `Layout` for mute, and preserve structured login API messages in `Login`.

- [ ] **Step 6: Run focused tests and commit**

```powershell
npm.cmd test -- --run src/services/moderationApi.test.ts src/components/forum/ModerationModal.test.tsx
Set-Location ..
git add frontend/src
git commit -m "Add forum moderation controls"
```

### Task 7: Simplify Forum List and Add Topic Pinning

**Files:**
- Modify: `frontend/src/pages/Forum.tsx`
- Modify: `frontend/src/components/forum/ForumTopicTable.tsx`
- Delete from imports/use only: `frontend/src/components/forum/ForumComposer.tsx`
- Test locally: `frontend/src/pages/Forum.test.tsx`

- [ ] **Step 1: Update local tests first**

Assert there is no breadcrumb, no `Nova tema` button/form, moderators/admins see pin controls, registered users do not, and pin refreshes the list.

- [ ] **Step 2: Run and verify failure**

```powershell
Set-Location frontend
npm.cmd test -- --run src/pages/Forum.test.tsx
```

Expected: breadcrumb and composer assertions fail.

- [ ] **Step 3: Remove composer state and rendering**

Delete `ForumComposer`, `Plus`, authentication-only topic creation branches, `composerOpen`, and `createTopic` from the page. Keep search, loading, error, table, and pagination behavior.

- [ ] **Step 4: Add compact topic pin actions**

Pass current role and an async pin toggle to `ForumTopicTable`. Render an icon button on the right only when `currentRole` outranks `topic.autorUloga`; use optimistic state with rollback and an accessible label `Pinuj temu` or `Otkači temu`.

- [ ] **Step 5: Run focused tests and commit**

```powershell
npm.cmd test -- --run src/pages/Forum.test.tsx
Set-Location ..
git add frontend/src/pages/Forum.tsx frontend/src/components/forum/ForumTopicTable.tsx
git commit -m "Simplify forum list and add topic pins"
```

### Task 8: Build Unlimited Thread Rails and Comment Moderation

**Files:**
- Modify: `frontend/src/utils/forumTree.ts`
- Modify: `frontend/src/types/api.ts`
- Modify: `frontend/src/components/forum/ForumThread.tsx`
- Modify: `frontend/src/components/forum/ForumComment.tsx`
- Modify: `frontend/src/pages/ForumDiscussion.tsx`
- Create: `frontend/src/components/forum/CommentActionsMenu.tsx`
- Test locally: `frontend/src/utils/forumTree.test.ts`
- Test locally: `frontend/src/pages/ForumDiscussion.test.tsx`

- [ ] **Step 1: Write failing unlimited-depth and pinned-subtree tests**

Build eight nested comments and assert depths `1..8`, visual indent caps at level six, level seven displays parent reference, pinned subtree appears once before normal roots, and stable numbers are preserved.

- [ ] **Step 2: Run and verify failure**

```powershell
Set-Location frontend
npm.cmd test -- --run src/utils/forumTree.test.ts src/pages/ForumDiscussion.test.tsx
```

Expected: current depth is capped at three and no moderation controls exist.

- [ ] **Step 3: Replace capped depth with unlimited logical depth**

Remove `Math.min(..., 3)`. Keep `depth` logical and compute `visualDepth = Math.min(depth, 6)` in `ForumComment`. Use inline `marginLeft: (visualDepth - 1) * 8` plus the approved thin left rail; for depth above six render `odgovor na #${parentBroj}`.

- [ ] **Step 4: Separate pinned roots without duplication**

Build the normal tree once, find pinned node IDs, detach pinned nodes from their parent/root list, then render pinned subtrees first followed by remaining roots. Preserve each node's backend number.

- [ ] **Step 5: Add role-aware compact actions**

Make eligible usernames buttons that open `ModerationModal`. Add right-aligned `CommentActionsMenu` for pin/unpin and delete. Use backend errors as final authority, refresh comments after success, and preserve optimistic vote behavior.

- [ ] **Step 6: Run focused tests and mobile visual check**

Run Step 2, then capture a 390 x 844 screenshot and verify no horizontal document overflow, no duplicated pinned comment, visible rails, and usable action buttons.

- [ ] **Step 7: Commit**

```powershell
Set-Location ..
git add frontend/src
git commit -m "Add unlimited moderated forum threads"
```

### Task 9: Make Relative and Match Completion Times Truthful

**Files:**
- Create: `frontend/src/components/RelativeTime.tsx`
- Modify: `frontend/src/utils/relativeTime.ts`
- Modify: `frontend/src/components/forum/ForumComment.tsx`
- Modify: `frontend/src/components/forum/ForumTopicTable.tsx`
- Modify: `frontend/src/pages/ForumDiscussion.tsx`
- Modify: `frontend/src/components/MatchRow.tsx`
- Modify: `frontend/src/types/api.ts`
- Modify: `backend/PLeagueHub.Api/Requests/CreateMatchRequest.cs`
- Modify: `backend/PLeagueHub.Api/Requests/UpdateMatchRequest.cs`
- Modify: `backend/PLeagueHub.Api/Controllers/MatchesController.cs`
- Modify: `backend/PLeagueHub.Api/Data/Seeding/DatabaseSeeder.cs`
- Test locally: `frontend/src/utils/relativeTime.test.ts`
- Test locally: `backend/PLeagueHub.Api.Tests/MatchWriteEndpointsTests.cs`

- [ ] **Step 1: Write failing timing tests**

Assert exact hour flooring, future timestamps never produce fabricated elapsed time, `RelativeTime` refreshes after one minute with fake timers, and match create/update round-trips `zavrsenaAt` without deriving it from kickoff.

- [ ] **Step 2: Run focused tests and verify failure**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter MatchWriteEndpointsTests --artifacts-path "$env:TEMP\pleaguehub-artifacts"
Set-Location frontend
npm.cmd test -- --run src/utils/relativeTime.test.ts
```

- [ ] **Step 3: Add completion timestamp to match write contracts**

Accept nullable `ZavrsenaAt` in create/update requests and persist it unchanged. Do not infer it from `Datum`, score, or status. The current `IFootballProvider` has no match synchronization contract, so this task prepares accurate storage without inventing an undocumented RapidAPI endpoint.

- [ ] **Step 4: Add the refreshing component**

Implement a component that renders `formatRelativeTime(value, now)` and updates `now` every 60 seconds, clearing the interval on unmount. Replace direct forum calls with this component.

- [ ] **Step 5: Render finished matches accurately**

Extend frontend `Match` with `zavrsenaAt?: string | null`. Render `FT` and, only when set, a secondary `RelativeTime` label. Otherwise render only `FT`.

- [ ] **Step 6: Make forum seed timestamps relative to seed execution**

Capture one `var seedNow = DateTime.UtcNow;` and express demo topic/comment timestamps with `seedNow.AddHours(...)`/`AddMinutes(...)`, preserving order while avoiding permanently stale labels.

- [ ] **Step 7: Run focused tests and commit**

Run Step 2 again. Expected: all focused tests pass.

```powershell
Set-Location ..
git add backend/PLeagueHub.Api frontend/src
git commit -m "Use accurate forum and match timestamps"
```

### Task 10: Integrated Verification and Push

**Files:**
- Verify all changed files

- [ ] **Step 1: Run all local backend tests**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --artifacts-path "$env:TEMP\pleaguehub-artifacts-final"
```

Expected: zero failures.

- [ ] **Step 2: Run all local frontend tests and production build**

```powershell
Set-Location frontend
npm.cmd test -- --run
npm.cmd run build
Set-Location ..
```

Expected: zero failures and successful Vite build.

- [ ] **Step 3: Verify real MongoDB and Swagger flows**

With Docker MongoDB and the API running, verify: moderator cannot act on admin/moderator; admin can mute/suspend moderator; mute blocks comment/vote; suspension blocks login; revoke restores access; topic/comment pin is idempotent; delete preserves parent placeholder; all approved Swagger paths exist.

- [ ] **Step 4: Verify desktop and mobile forum UI**

Inspect the forum list, six-plus nested levels, moderation modal, pin ordering, deleted parent, mute notice, and 390 px viewport. Confirm document width equals viewport width.

- [ ] **Step 5: Inspect Git state**

```powershell
git diff --check
git status --short
git check-ignore frontend/src/pages/Forum.test.tsx
git log --oneline origin/master..HEAD
```

Expected: no whitespace errors, tests remain ignored, and only intended implementation commits are ahead.

- [ ] **Step 6: Push master after verification**

```powershell
git push origin master
```
