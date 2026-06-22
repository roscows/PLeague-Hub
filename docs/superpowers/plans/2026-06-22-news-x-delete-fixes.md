# News X and Deletion Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent duplicate X embeds, allow deleted news URLs to be republished, and remove the redundant Premier League filter.

**Architecture:** Keep soft deletion and the current sparse unique indexes. Release a deleted post's external identity fields while preserving their previous values in the existing editorial audit snapshot, and isolate each X widget render in its own disposable mount element.

**Tech Stack:** ASP.NET Core .NET 10, MongoDB.Driver, xUnit, React 19, TypeScript, Vitest, Testing Library, Tailwind CSS

---

### Task 1: Deleted news can be republished

**Files:**
- Modify: `backend/PLeagueHub.Api/Services/NewsService.cs`
- Modify: `backend/PLeagueHub.Api/Repositories/MongoNewsRepository.cs`
- Test: `backend/PLeagueHub.Api.Tests/NewsServiceTests.cs`
- Test: `backend/PLeagueHub.Api.Tests/NewsRepositoryTests.cs`

- [ ] **Step 1: Write failing service and repository tests**

Add a service assertion that `DeleteAsync` saves a post with `Obrisan == true` and null `OriginalUrl`, `XEmbedUrl`, `ExternalId`, and `Fingerprint`. Assert that the recorded `brisanje_vesti` audit event's `Staro` snapshot still contains the original URL. Add a Mongo repository test that inserts an `Obrisan == true` post with a matching fingerprint and expects `FindDuplicateAsync` to return `null`.

- [ ] **Step 2: Run focused backend tests and verify RED**

Run:
```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~NewsServiceTests|FullyQualifiedName~NewsRepositoryTests"
```
Expected: failures because deletion retains external identity fields and duplicate lookup includes deleted posts.

- [ ] **Step 3: Implement the minimal backend fix**

In `DeleteAsync`, preserve `old` before changing the post, then set:
```csharp
post.Obrisan = true;
post.OriginalUrl = null;
post.XEmbedUrl = null;
post.ExternalId = null;
post.Fingerprint = null;
post.UpdatedAt = UtcNow();
```

In `FindDuplicateAsync`, require an active news post:
```csharp
posts.Eq(post => post.Tip, "vest")
& posts.Eq(post => post.Obrisan, false)
& duplicate
```

- [ ] **Step 4: Run focused backend tests and verify GREEN**

Run the command from Step 2. Expected: all selected tests pass.

- [ ] **Step 5: Commit production changes only**

```powershell
git add backend/PLeagueHub.Api/Services/NewsService.cs backend/PLeagueHub.Api/Repositories/MongoNewsRepository.cs
git commit -m "Allow republishing deleted news links"
```

### Task 2: X embed has one visible widget

**Files:**
- Modify: `frontend/src/components/news/XEmbed.tsx`
- Test: `frontend/src/components/news/XEmbed.test.tsx`

- [ ] **Step 1: Write a failing StrictMode regression test**

Render `<XEmbed url="https://x.com/arsenal/status/123456" />` inside `React.StrictMode`. Mock `createTweet` so it appends a marker element to the supplied mount after a resolved promise. Assert that the visible `aria-label="X objava"` container contains exactly one current mount/marker after effects settle.

- [ ] **Step 2: Run the focused frontend test and verify RED**

Run:
```powershell
npm.cmd test -- --run src/components/news/XEmbed.test.tsx
```
Expected: failure because both StrictMode effect executions write into the same visible container.

- [ ] **Step 3: Isolate each widget execution**

At the start of the effect, create and append a dedicated mount:
```tsx
const host = container.current;
const mount = document.createElement('div');
host.replaceChildren(mount);
```

Pass `mount` to `widgets.createTweet`. During cleanup, remove `mount` only if it is still connected to `host`. Stale widget completion then targets a detached node.

- [ ] **Step 4: Run the focused frontend test and verify GREEN**

Run the command from Step 2. Expected: one visible marker and a passing test.

### Task 3: Remove the redundant category filter

**Files:**
- Modify: `frontend/src/components/news/NewsFilters.tsx`
- Test: `frontend/src/pages/News.test.tsx`

- [ ] **Step 1: Add a failing filter assertion**

In the existing timeline test, assert that no button named `Premier liga` is rendered, while `Sve`, `Transferi`, `FPL`, and `Klubovi` remain present.

- [ ] **Step 2: Run the focused test and verify RED**

Run:
```powershell
npm.cmd test -- --run src/pages/News.test.tsx
```
Expected: failure because the `Premier liga` button is still rendered.

- [ ] **Step 3: Remove only the redundant filter entry**

Delete this entry from the `categories` array:
```tsx
{ value: 'premier_league', label: 'Premier liga' },
```

Do not remove `premier_league` from API types, editors, stored records, or backend validation.

- [ ] **Step 4: Run focused frontend tests and verify GREEN**

Run both frontend tests from Tasks 2 and 3. Expected: all pass.

- [ ] **Step 5: Commit production changes only**

```powershell
git add frontend/src/components/news/XEmbed.tsx frontend/src/components/news/NewsFilters.tsx
git commit -m "Fix X embeds and simplify news filters"
```

### Task 4: Full verification and delivery

**Files:**
- No production file changes expected

- [ ] **Step 1: Run all verification commands**

```powershell
dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --no-restore
npm.cmd test -- --run
npm.cmd run build
git diff --check
git status --short
```

Expected: all backend and frontend tests pass, Vite production build succeeds, no whitespace errors, and only intentionally ignored local test changes remain outside Git status.

- [ ] **Step 2: Verify the real API flow**

Create an X news item as admin, delete it, and create the same normalized X URL again. Expected: the second creation returns success rather than `409 Conflict`. Delete the temporary item afterwards.

- [ ] **Step 3: Push commits**

```powershell
git push origin master
```

Expected: local `HEAD` equals `origin/master`.
