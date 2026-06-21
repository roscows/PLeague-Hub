# Team Logo Cache Design

## Goal

Display a valid crest for every synchronized Premier League team without exposing the RapidAPI key or consuming provider quota on every frontend render.

## Root Cause

FootAPI standings include team identifiers but no logo URL. Newly synchronized teams therefore have an empty `logo_url`, while the frontend renders an image element without a source. FootAPI provides PNG data through `/api/team/{providerId}/image` when called with server-side RapidAPI credentials.

## Architecture

Logo synchronization is an explicit administrator operation separate from standings synchronization:

`POST /api/integrations/football/sync/team-logos`

The backend loads teams with a positive `ProviderId`, downloads missing logos through `IFootballProvider`, stores them under `wwwroot/team-logos/{providerId}.png`, and updates each team's `LogoUrl` to `/team-logos/{providerId}.png`. ASP.NET Core static-file middleware serves the cached files.

The frontend resolves API-relative asset paths against `VITE_API_BASE_URL`. Existing absolute URLs remain supported. A shared logo component displays a restrained fallback only after a missing or failed image is detected.

## Provider Client

`IFootballProvider` gains `GetTeamLogoAsync(int providerId)`, returning immutable image bytes and content type. `FootApiClient` calls `/api/team/{providerId}/image` with the existing server-side RapidAPI headers.

The client accepts only successful `image/*` responses no larger than 1 MB. Empty, oversized, or non-image responses fail without creating a cache file.

## Cache Rules

- Cache directory: `backend/PLeagueHub.Api/wwwroot/team-logos`
- File name: positive numeric provider ID plus `.png`
- Runtime PNG files are ignored by Git; a `.gitkeep` preserves the directory.
- Existing non-empty cache files are skipped without a provider request.
- Files are written to a temporary file in the same directory and atomically renamed after validation.
- Provider IDs, rather than user-controlled path fragments, determine every file name.

The service processes downloads sequentially with at least 275 ms between provider requests, remaining below the free-plan limit of four requests per second.

## Synchronization Behavior

All teams with a positive provider ID are considered. For each team:

1. If its cache file exists and is non-empty, update `LogoUrl` only when necessary and count it as skipped.
2. Otherwise download, validate, atomically cache, update `LogoUrl`, and count it as downloaded.
3. If a single logo fails, record the provider ID as failed and continue with other teams.

The response contains `downloaded`, `updated`, `skipped`, and `failed` counts plus the failed provider IDs. Re-running a completed synchronization consumes no RapidAPI requests.

## API and Security

The endpoint requires JWT role `administrator` and appears in Swagger. Missing or non-administrator tokens return `401` or `403`. A completed operation returns `200`, including partial failures in its summary so an administrator can retry. Unexpected service-level failures return `502` without exposing provider response bodies or credentials.

Static logo files are public because team crests are public portal assets. No API key or provider header reaches the browser.

## Frontend

A single `TeamLogo` component replaces direct team `<img>` usage in the layout, home, profile, statistics, match rows, and search results where applicable. It:

- resolves `/team-logos/...` against the configured backend base URL;
- preserves absolute `http`, `https`, and data URLs;
- uses an empty-alt decorative image when available;
- displays a stable neutral placeholder when the source is empty or loading fails.

## Testing

- Provider-client tests verify route, RapidAPI headers, content type, size limit, and returned bytes.
- Logo-sync service tests verify download, cache skip, URL repair, atomic file outcome, provider failure continuation, and rate-delay abstraction.
- Endpoint tests verify `401`, `403`, success summary, and `502` handling.
- Frontend tests verify API-relative URL resolution and absolute URL preservation.
- Final verification runs all backend tests, frontend tests, frontend production build, a real logo sync, and an HTTP request for one cached PNG.

## Deferred Work

Distributed object storage and multi-instance shared caches are deferred until deployment architecture requires them. The local cache is appropriate for the current single-instance development setup.
