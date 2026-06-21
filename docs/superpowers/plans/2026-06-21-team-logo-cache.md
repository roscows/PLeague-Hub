# Team Logo Cache Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Download every synchronized team crest once through FootAPI, cache it locally, and render it consistently throughout the React frontend.

**Architecture:** `FootApiClient` returns validated image data. `TeamLogoSyncService` coordinates teams, a request pacer, and `LocalTeamLogoCache`; the controller exposes an administrator command. React resolves backend-relative assets through one helper and renders them through one resilient component.

**Tech Stack:** .NET 10, ASP.NET Core static files, HttpClient, MongoDB repository, React 19, TypeScript, Vitest, xUnit

---

### Task 1: FootAPI Logo Client

**Files:**
- Create: `backend/PLeagueHub.Api/Services/Football/FootballTeamLogo.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/IFootballProvider.cs`
- Modify: `backend/PLeagueHub.Api/Services/Football/FootApiClient.cs`
- Test: `backend/PLeagueHub.Api.Tests/FootApiClientTests.cs`

- [ ] Add a failing test that calls `GetTeamLogoAsync(60)`, verifies `/api/team/60/image` and RapidAPI headers, and expects PNG bytes.
- [ ] Add failing tests rejecting `text/html`, empty data, and content larger than 1 MB.
- [ ] Run focused tests and confirm RED because the method is missing.
- [ ] Create:

```csharp
public sealed record FootballTeamLogo(byte[] Content, string ContentType);
```

- [ ] Add this provider contract:

```csharp
Task<FootballTeamLogo> GetTeamLogoAsync(
    int providerId,
    CancellationToken cancellationToken = default);
```

- [ ] Implement the request with `ResponseHeadersRead`, require `image/*`, read at most `1_048_577` bytes, and throw `InvalidDataException` for invalid content.
- [ ] Run `FootApiClientTests`; expect all tests GREEN.

### Task 2: Atomic Cache and Logo Sync Service

**Files:**
- Create: `backend/PLeagueHub.Api/Services/Football/ITeamLogoCache.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/LocalTeamLogoCache.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/IProviderRequestPacer.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/ProviderRequestPacer.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/TeamLogoSyncService.cs`
- Create: `backend/PLeagueHub.Api/Services/Football/TeamLogoSyncException.cs`
- Create: `backend/PLeagueHub.Api/Responses/TeamLogoSyncResponse.cs`
- Test: `backend/PLeagueHub.Api.Tests/TeamLogoSyncServiceTests.cs`
- Test: `backend/PLeagueHub.Api.Tests/LocalTeamLogoCacheTests.cs`

- [ ] Write failing service tests for downloading missing logos, skipping an existing cache file without a provider call, repairing a stale `LogoUrl`, and continuing after one provider failure.
- [ ] Write a failing cache test using a temporary content root; after `SaveAsync`, only `{providerId}.png` may remain and its bytes must match.
- [ ] Confirm focused tests RED because cache and service types are missing.
- [ ] Define the cache contract:

```csharp
public interface ITeamLogoCache
{
    bool Exists(int providerId);
    Task SaveAsync(int providerId, FootballTeamLogo logo, CancellationToken cancellationToken = default);
    string GetPublicUrl(int providerId);
}
```

- [ ] Implement `LocalTeamLogoCache` under `IWebHostEnvironment.WebRootPath/team-logos`, map validated PNG/WebP/JPEG types to their correct extension, and use a GUID temporary file with `File.Move(temp, final, true)` only after a complete write.
- [ ] Define `IProviderRequestPacer.WaitAsync` and implement a `SemaphoreSlim`-protected minimum 275 ms interval using `TimeProvider.System`.
- [ ] Return this summary:

```csharp
public sealed record TeamLogoSyncResponse(
    int Downloaded,
    int Updated,
    int Skipped,
    int Failed,
    IReadOnlyCollection<int> FailedProviderIds);
```

- [ ] Implement `TeamLogoSyncService` over teams with positive provider IDs. Skip cached files, pace only actual downloads, continue on `HttpRequestException`, `InvalidDataException`, or provider configuration errors, update `LogoUrl`, and count results deterministically.
- [ ] Wrap failures that prevent the whole operation, such as loading teams from the repository, in `TeamLogoSyncException`; per-team provider failures remain in the `200` summary.
- [ ] Run cache and sync tests; expect GREEN.

### Task 3: Admin Endpoint and Static Files

**Files:**
- Modify: `backend/PLeagueHub.Api/Program.cs`
- Modify: `backend/PLeagueHub.Api/Controllers/IntegrationsController.cs`
- Modify: `backend/PLeagueHub.Api.Tests/IntegrationEndpointsTests.cs`
- Modify: `.gitignore`
- Create: `backend/PLeagueHub.Api/wwwroot/team-logos/.gitkeep`

- [ ] Add failing endpoint tests for unauthenticated `401`, moderator `403`, administrator `200` summary, and service-level `502`.
- [ ] Confirm RED because `/api/integrations/football/sync/team-logos` is missing.
- [ ] Register `ITeamLogoCache`, `IProviderRequestPacer`, and `TeamLogoSyncService` and add `app.UseStaticFiles()` before authentication.
- [ ] Add administrator-only `POST sync/team-logos` to `IntegrationsController`, returning `TeamLogoSyncResponse` and mapping `TeamLogoSyncException` to `502`.
- [ ] Ignore `backend/PLeagueHub.Api/wwwroot/team-logos/*` while negating `.gitkeep`.
- [ ] Run endpoint tests; expect GREEN and verify the route in Swagger JSON.

### Task 4: Shared React Logo Rendering

**Files:**
- Create: `frontend/src/services/assets.ts`
- Create: `frontend/src/services/assets.test.ts`
- Create: `frontend/src/components/TeamLogo.tsx`
- Modify: `frontend/src/components/TeamIdentity.tsx`
- Modify: `frontend/src/components/Layout.tsx`
- Modify: `frontend/src/components/GlobalSearch.tsx`
- Modify: `frontend/src/pages/Home.tsx`
- Modify: `frontend/src/pages/Profile.tsx`
- Modify: `frontend/src/pages/Stats.tsx`
- Modify: `frontend/src/types/api.ts`

- [ ] Write failing Vitest cases: `/team-logos/60.png` resolves against `VITE_API_BASE_URL`; `https://...`, `data:...`, and empty strings remain valid/predictable.
- [ ] Implement:

```ts
export function resolveApiAssetUrl(value?: string): string {
  if (!value) return '';
  if (/^(https?:|data:)/i.test(value)) return value;
  return new URL(value, `${API_BASE_URL.replace(/\/$/, '')}/`).toString();
}
```

- [ ] Add optional `providerId?: number | null` to `Team`.
- [ ] Implement `TeamLogo` with resolved source, `onError` state reset on source change, fixed dimensions, and a neutral `Shield` fallback.
- [ ] Replace every direct team-logo `<img>` in layout, search, home, profile, stats, and `TeamIdentity` with `TeamLogo`.
- [ ] Run `npm.cmd test` and `npm.cmd run build`; expect GREEN.

### Task 5: End-to-End Verification

**Files:**
- Modify only files above if verification reveals a covered defect.

- [ ] Run all backend tests and all frontend tests/build.
- [ ] Run the administrator logo sync once; expect cached PNG files and no failed provider IDs.
- [ ] Run it again; expect zero provider downloads and all teams skipped.
- [ ] Request one `/team-logos/{providerId}.png`; require `200`, `image/png`, and non-zero bytes.
- [ ] Verify MongoDB has 20 non-empty local `logo_url` values and no duplicate provider IDs.
- [ ] Open the profile in the browser and confirm every crest renders without empty squares on desktop and mobile widths.
- [ ] Run `git diff --check` and verify the real RapidAPI key is absent from tracked files and diff.
