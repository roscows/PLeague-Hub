# Header Favorite Team and Profile Removal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Profile page with an authenticated header control for one favorite team and display that team's crest beside forum and news comment authors.

**Architecture:** Preserve the existing `favoritniTimovi` array and API shape while enforcing a zero-or-one invariant and migrating legacy arrays. Extend the shared comment response with an optional team summary, then reuse one focused header menu component and the existing `ForumComment` component across the frontend.

**Tech Stack:** ASP.NET Core .NET 10, MongoDB.Driver, C#, React 19, TypeScript, React Router, Tailwind CSS, Vitest, Testing Library, xUnit

---

### Task 1: Enforce one favorite team and migrate legacy users

**Files:**
- Create: `backend/PLeagueHub.Api/Data/FavoriteTeamMigration.cs`
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Modify: `backend/PLeagueHub.Api/Controllers/UsersController.cs`
- Modify: `backend/PLeagueHub.Api/Data/Seeding/DatabaseSeeder.cs`
- Test: `backend/PLeagueHub.Api.Tests/UserProfileEndpointsTests.cs`
- Test: `backend/PLeagueHub.Api.Tests/FavoriteTeamMigrationTests.cs`

- [ ] **Step 1: Write failing API and migration tests**

Add an endpoint test that sends two distinct valid team IDs to `PUT /api/users/me/favorite-teams` and expects `400 Bad Request`. Keep existing zero-team and one-team success coverage. Add a Mongo integration test that inserts a user with three IDs, runs `FavoriteTeamMigration.MigrateAsync()` twice, and asserts the stored array is `[firstId]` after both runs and the second call reports zero modifications.

- [ ] **Step 2: Run focused backend tests and verify RED**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~UserProfileEndpointsTests|FullyQualifiedName~FavoriteTeamMigrationTests"
```

Expected: the endpoint accepts multiple teams and `FavoriteTeamMigration` does not exist.

- [ ] **Step 3: Implement the idempotent migration**

Create `FavoriteTeamMigration` with `MongoContext` and a pipeline update equivalent to:

```csharp
var filter = Builders<User>.Filter.Where(user => user.FavoritniTimovi.Count > 1);
var update = new PipelineUpdateDefinition<User>(
[
    new BsonDocument("$set", new BsonDocument
    {
        ["favoritniTimovi"] = new BsonDocument("$slice", new BsonArray
        {
            new BsonDocument("$ifNull", new BsonArray { "$favoritniTimovi", new BsonArray() }),
            1
        })
    })
]);
```

Return `ModifiedCount`. Register it as a singleton in `Program.cs` and call it before index initialization, beside `NewsMetadataMigration`.

- [ ] **Step 4: Enforce the API invariant and update seed data**

After trimming and deduplicating request IDs in `UsersController`, return:

```csharp
if (teamIds.Count > 1)
    return BadRequest(new { message = "Moguce je izabrati najvise jedan omiljeni tim." });
```

Keep existing team existence validation. Change the seeded `fan` user to contain only the first current team ID.

- [ ] **Step 5: Run focused backend tests and verify GREEN**

Run the command from Step 2. Expected: all selected tests pass.

- [ ] **Step 6: Commit production backend invariant files only**

```powershell
git add backend/PLeagueHub.Api/Data/FavoriteTeamMigration.cs backend/PLeagueHub.Api/Program.cs backend/PLeagueHub.Api/Controllers/UsersController.cs backend/PLeagueHub.Api/Data/Seeding/DatabaseSeeder.cs
git commit -m "Enforce one favorite team per user"
```

### Task 2: Include favorite-team identity in shared comments

**Files:**
- Modify: `backend/PLeagueHub.Api/Responses/ForumCommentResponse.cs`
- Modify: `backend/PLeagueHub.Api/Services/CommentService.cs`
- Test: `backend/PLeagueHub.Api.Tests/CommentServiceTests.cs`

- [ ] **Step 1: Write failing comment mapping tests**

Extend the existing service test fixtures with a user whose first favorite ID resolves to an Arsenal `Team`. Assert both loaded and newly created comment responses contain:

```csharp
Assert.Equal("team-1", response.AutorFavoritniTim?.Id);
Assert.Equal("Arsenal", response.AutorFavoritniTim?.Naziv);
Assert.Equal("/team-logos/42.png", response.AutorFavoritniTim?.LogoUrl);
```

Add a missing-team case and assert `AutorFavoritniTim` is null.

- [ ] **Step 2: Run comment service tests and verify RED**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~CommentServiceTests"
```

Expected: compile/test failure because the response has no favorite-team summary.

- [ ] **Step 3: Extend the response contract**

Add:

```csharp
public sealed record FavoriteTeamResponse(string Id, string Naziv, string LogoUrl);
```

and append `FavoriteTeamResponse? AutorFavoritniTim` to `ForumCommentResponse`.

- [ ] **Step 4: Resolve teams once per unique author selection**

Add optional `IRepository<Team>` injection to `CommentService` without breaking existing direct test constructors. Build a dictionary for distinct first favorite IDs using `GetByIdAsync`. Pass that dictionary into `MapComment` for both list and create flows. Map null when the user, favorite ID, or team is missing.

- [ ] **Step 5: Run comment service tests and verify GREEN**

Run the command from Step 2. Expected: all selected tests pass.

- [ ] **Step 6: Commit production comment contract files only**

```powershell
git add backend/PLeagueHub.Api/Responses/ForumCommentResponse.cs backend/PLeagueHub.Api/Services/CommentService.cs
git commit -m "Expose favorite teams with comments"
```

### Task 3: Build the header favorite-team control

**Files:**
- Create: `frontend/src/components/FavoriteTeamMenu.tsx`
- Modify: `frontend/src/components/Layout.tsx`
- Modify: `frontend/src/types/api.ts`
- Test: `frontend/src/components/FavoriteTeamMenu.test.tsx`
- Test: `frontend/src/components/Layout.test.tsx`

- [ ] **Step 1: Write failing menu and header tests**

Require these behaviors:

- no selection renders a white `Shield` inside the dark square button;
- the button opens a radio-style list with `Bez omiljenog kluba` and all teams;
- selecting a team calls `usersApi.updateFavoriteTeams([teamId])`, refreshes auth profile, and closes;
- clearing calls `usersApi.updateFavoriteTeams([])`;
- failed saving keeps the menu open and shows an inline error;
- outside click and Escape close the menu;
- registered users show `Clan od 22.06.2026.`;
- moderators show `Moderator`, administrators show `Administrator`.

- [ ] **Step 2: Run focused component tests and verify RED**

```powershell
npm.cmd test -- --run src/components/FavoriteTeamMenu.test.tsx src/components/Layout.test.tsx
```

Expected: failure because the menu component and new header labels do not exist.

- [ ] **Step 3: Implement `FavoriteTeamMenu`**

Use a root `ref`, `open`, `pending`, and `error` state. The trigger has `aria-haspopup="listbox"` and `aria-expanded`. Render `Shield` directly when `selectedTeam` is absent; do not render an empty image. For selected teams, render `TeamLogo` inside the same square frame.

On a row selection:

```tsx
setPending(true);
setError(null);
try {
  await usersApi.updateFavoriteTeams(teamId ? [teamId] : []);
  await refreshProfile();
  setOpen(false);
} catch (requestError) {
  setError(getApiErrorMessage(requestError, 'Omiljeni klub nije sacuvan.'));
} finally {
  setPending(false);
}
```

Add document `pointerdown` and `keydown` listeners only while open, and remove them in effect cleanup.

- [ ] **Step 4: Integrate the control into `Layout`**

Remove `UserCircle` and the `/profile` navigation item. Find the selected team with `user?.favoritniTimovi[0]`. Place `FavoriteTeamMenu` before username. Format registered-user membership with:

```tsx
new Intl.DateTimeFormat('sr-Latn-RS', {
  day: '2-digit', month: '2-digit', year: 'numeric'
}).format(new Date(user.datumReg))
```

Normalize separators to dots so the result is `Clan od dd.MM.yyyy.`. For moderator and administrator, render their role labels instead.

- [ ] **Step 5: Run focused component tests and verify GREEN**

Run the command from Step 2. Expected: all selected tests pass.

### Task 4: Remove Profile and show club crests in comments

**Files:**
- Delete: `frontend/src/pages/Profile.tsx`
- Modify: `frontend/src/App.tsx`
- Modify: `frontend/src/pages/Login.tsx`
- Modify: `frontend/src/pages/Register.tsx`
- Modify: `frontend/src/types/api.ts`
- Modify: `frontend/src/components/forum/ForumComment.tsx`
- Test: `frontend/src/pages/Login.test.tsx`
- Test: `frontend/src/pages/Register.test.tsx`
- Test: `frontend/src/components/forum/ForumComment.test.tsx`

- [ ] **Step 1: Write failing navigation and comment tests**

Update login/register tests to expect `/` when no `location.state.from` exists while retaining protected return-route assertions. Add comment tests that assert a provided `autorFavoritniTim` crest is rendered before the username with the team name as tooltip, no crest is rendered when null, no `Clan` badge exists for `registrovani`, and moderator/administrator badges remain.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
npm.cmd test -- --run src/pages/Login.test.tsx src/pages/Register.test.tsx src/components/forum/ForumComment.test.tsx
```

Expected: default redirects still target `/profile` and comments lack favorite-team rendering.

- [ ] **Step 3: Remove the Profile route and page**

Delete the Profile import and protected `/profile` route from `App.tsx`, then delete `Profile.tsx`. Keep `ProtectedRoute` for moderator/administrator source management.

- [ ] **Step 4: Change default authentication redirects**

In both `Login.tsx` and `Register.tsx`, change only the fallback destination:

```tsx
const redirectTo = state?.from?.pathname ?? '/';
```

- [ ] **Step 5: Extend frontend comment types and rendering**

Add:

```tsx
interface FavoriteTeamSummary {
  id: string;
  naziv: string;
  logoUrl: string;
}
```

and `autorFavoritniTim: FavoriteTeamSummary | null` to `ForumComment`. In the author area, render `TeamLogo` at `size-5` inside a `span` with `title={team.naziv}` before the username. Render the role badge only when `autorUloga !== 'registrovani'`.

- [ ] **Step 6: Run focused tests and verify GREEN**

Run the command from Step 2. Expected: all selected tests pass.

- [ ] **Step 7: Commit production frontend files only**

```powershell
git add frontend/src/components/FavoriteTeamMenu.tsx frontend/src/components/Layout.tsx frontend/src/components/forum/ForumComment.tsx frontend/src/types/api.ts frontend/src/App.tsx frontend/src/pages/Login.tsx frontend/src/pages/Register.tsx
git add -u frontend/src/pages/Profile.tsx
git commit -m "Move favorite team identity into the header"
```

### Task 5: Full integration and delivery

**Files:**
- No production file changes expected

- [ ] **Step 1: Run complete verification**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --no-restore
npm.cmd test -- --run
npm.cmd run build
git diff --check
git status --short
```

Expected: all backend/frontend tests pass, production build succeeds, no whitespace errors, and ignored tests remain absent from Git status.

- [ ] **Step 2: Verify real API behavior**

Authenticate as a seeded user, save one favorite, verify `/api/users/me`, load forum/news comments and confirm the favorite summary, clear the favorite, and confirm the summary disappears. Send two team IDs and confirm `400 Bad Request`. Restore the seeded user's intended favorite after verification.

- [ ] **Step 3: Verify local UI behavior**

Check desktop and mobile header layout, default white shield, immediate save/clear, membership/role labels, missing-team tolerance, no Profile navigation, home redirects, and crest placement in both forum and news comments.

- [ ] **Step 4: Push master**

```powershell
git push origin master
```

Expected: local `HEAD` equals `origin/master`.
