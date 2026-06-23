# Standings Table ("Tabela") — Design Spec

**Date:** 2026-06-23
**Status:** Approved (design), pending implementation plan
**Author:** Marko + Claude

## Goal

Add a new top-level navigation entry **"Tabela"** (between *Statistike* and *Vesti*)
that opens a full Premier League standings table. The table shows the classic
columns and supports viewing **previous seasons** via a season selector.

## Context / Current State

- **Menu** is defined by the `navItems` array in
  `frontend/src/components/Layout.tsx:22`. Items render in both the desktop
  sidebar and the mobile horizontal nav from the same array.
- A **mini standings widget** already exists in the right sidebar
  (`Layout.tsx:149`), driven by `teamsApi.list()` (`GET /api/teams`). The `Team`
  model only carries `pozicija` and `bodovi` (`frontend/src/types/api.ts:40`) —
  no played/wins/draws/losses/goals columns.
- **Matches** (`GET /api/matches`, public) carry `sezona`, `kolo`,
  `golDomacin/golGost`, `status` (`MatchesController.cs:24`). The seeder only
  seeds a few matches for a single season `"2026/27"`
  (`DatabaseSeeder.cs:289`) — so a match-derived table is incomplete and has no
  past-season data.
- **FootApi** exposes full standings via
  `/api/tournament/{tournamentId}/season/{seasonId}/standings/total`
  (`FootApiClient.cs:34`). The current parser reads only `Team`, `Position`,
  `Points` and ignores the per-row played/wins/draws/losses/goals fields that
  the upstream response contains. Tournament id for the Premier League is `17`
  (`TeamSyncService.cs:10`); the current default `seasonId` is `96668`.

## Decision: Data Source

**FootApi live + server-side cache.** A new public endpoint fetches full
standings from FootApi for the requested season, parses all classic columns,
and caches the result. No new MongoDB storage. This supports any season the
upstream API offers, is always fresh, and requires no admin sync step.

Rejected alternatives:
- *Stored per-season standings (admin sync):* more backend surface (model, repo,
  sync, per-season admin action); data can go stale mid-season. YAGNI for now.
- *Compute from matches:* impossible for past seasons (no data) and incomplete
  for the current season (partial fixtures seeded).

## Scope

### Columns (approved)
`Poz · Klub (logo + naziv) · OD · P · N · I · GF:GA · GR · Bod`
(Position, Club, Played, Wins, Draws, Losses, GoalsFor:GoalsAgainst,
GoalDifference, Points). `GR` (goal difference) and ordering are derived.

### Sort order
Points desc → Goal difference desc → Goals for desc (fallback to FootApi
`Position` if provided).

## Backend Design

### 1. Extend the standings model + parser
- `FootballTeamStanding` (`Services/Football/FootballTeamStanding.cs`) gains:
  `Played, Wins, Draws, Losses, GoalsFor, GoalsAgainst`. `GoalDifference` is a
  computed property (`GoalsFor - GoalsAgainst`).
- `FootApiClient.GetTeamStandingsAsync` parsing (`FootApiClient.cs:34`) reads the
  additional row fields (`matches`, `wins`, `draws`, `losses`, `scoresFor`,
  `scoresAgainst`). The private `FootApiStandingRow` record is extended
  accordingly.
- **Backward compatibility:** `TeamSyncService` keeps using only
  `Position`/`Points`/name — unaffected by the new fields.

### 2. Season list
- New provider method `GetSeasonsAsync(int tournamentId)` →
  `IReadOnlyCollection<FootballSeason(int Id, string Name, string Year)>` hitting
  `/api/tournament/{tournamentId}/seasons`.
- **Assumption to verify during implementation:** the exact FootApi seasons
  endpoint + response shape. If it differs, fall back to a small hard-coded list
  of known `{ seasonId, label }` pairs (current season guaranteed: `96668`).

### 3. StandingsController (public, no auth — read-only)
- `GET /api/standings/seasons` → `[{ seasonId, label }]`, cached long.
- `GET /api/standings?seasonId=` (default `96668`) →
  `[{ position, providerId, naziv, skracenica, logoUrl, odigrano, pobede,
  nereseno, porazi, datiGolovi, primljeniGolovi, golRazlika, bodovi }]`.
  - `logoUrl` resolved by joining each standings row to an existing `Team` doc
    on `ProviderId` (Team docs already carry `logoUrl` from the logo sync). Rows
    with no matching Team return empty `logoUrl`; the frontend `TeamLogo`
    fallback renders a placeholder.
  - Cache via `IMemoryCache`: current season short TTL (~10 min); past seasons
    long TTL (treated as immutable).
- DTOs: `SeasonResponse`, `StandingRowResponse`.

### 4. Errors
- FootApi failure → `502 Bad Gateway` with a message (consistent with
  `IntegrationsController` behavior). Frontend shows an error state.

## Frontend Design

### 1. Navigation
- Insert into `navItems` (`Layout.tsx:22`) between Statistike and Vesti:
  `{ to: '/tabela', label: 'Tabela', icon: ListOrdered }` (import from
  `lucide-react`). Renders automatically in both desktop + mobile nav.

### 2. Route + page
- `App.tsx`: add `<Route path="/tabela" element={<TablePage />} />` following the
  existing page-routing pattern.
- `pages/Table.tsx` (`TablePage`):
  - Season `<select>` defaulting to the current season; changing it refetches.
  - Full table with the approved columns; club cell uses `TeamLogo` + name.
  - States: loading, error (with retry), empty.
  - Responsive: on mobile hide `GF:GA` and `GR`; keep Poz / Klub / OD / Bod.

### 3. Services + types
- `services/standingsApi.ts`: `getSeasons()`, `getStandings(seasonId?)` using the
  shared `api` axios instance.
- `types/api.ts`: `Season` and `StandingRow` interfaces matching the DTOs.

## Out of Scope (YAGNI)
- Persisting standings in MongoDB.
- Live auto-refresh / websockets.
- Reconciling the mini sidebar widget with the new endpoint (it stays on
  `teamsApi` for now).
- Per-team form/last-5 indicators.

## Testing Strategy (TDD)
- **Backend (xUnit):**
  - Parser maps all classic columns from a representative fake `standings/total`
    JSON payload.
  - `GetSeasonsAsync` parses the seasons payload (or fallback list).
  - `StandingsController` returns rows for default + explicit season, surfaces
    `502` on provider failure, and serves cached results on repeat calls.
- **Frontend (vitest):**
  - `TablePage` renders rows from a mocked `standingsApi`.
  - Changing the season selector triggers a refetch with the new `seasonId`.
  - Loading and error states render correctly.

## Acceptance Criteria
1. "Tabela" appears in the menu between Statistike and Vesti, desktop + mobile,
   with a style-consistent icon and active-state highlight.
2. Navigating to `/tabela` shows a full standings table (all approved columns)
   for the current season.
3. A season selector lists available seasons; selecting a past season loads that
   season's full table.
4. Loading, error, and empty states are handled.
5. All new backend + frontend tests pass; existing suites remain green.
