# Match Detail + Statistics ("Detalj meča") — Design Spec

**Date:** 2026-06-25
**Status:** Approved (design), pending implementation plan
**Feature:** B of 2 (A = match ingestion, done; B = clickable match → detail page)

## Goal

Make every match row clickable, opening a Flashscore-style detail page that shows
the match header, team statistics, an incident timeline (goals, cards,
substitutions), and lineups — sourced live from FootApi by the stored match
provider id, fetched lazily and cached.

## Context / Current State

- `Match` (Mongo) now carries `ProviderId` (FootApi event id) for ingested
  matches; `/api/matches` returns it. 1142 finished matches have one.
- Frontend `Match` (`frontend/src/types/api.ts`) does **not** yet expose
  `providerId`. `MatchRow` (`frontend/src/components/MatchRow.tsx`) is a plain
  div (not clickable). No match-detail route exists in `App.tsx`.
- The standings feature established the live-fetch + `IMemoryCache` pattern
  (`StandingsService`) and the FootApi client/provider parse pattern.
- FootApi BASIC plan has a **daily request quota** (confirmed: ingestion
  exhausted it). Each match detail = 3 FootApi calls, so we must not pre-ingest
  all matches.

## FootApi Endpoints (paths confirmed from the playground)

All keyed by the event id (= `Match.ProviderId`):
- Statistics: `GET /api/match/{eventId}/statistics`
- Incidents:  `GET /api/match/{eventId}/incidents`
- Lineups:    `GET /api/match/{eventId}/lineups`

**Response shapes (SofaScore-standard — verify field names during
implementation, same approach used for matches):**
- *statistics:* `{ statistics: [ { period: "ALL", groups: [ { groupName,
  statisticsItems: [ { name, home, away } ] } ] } ] }` — use the `period: "ALL"`
  block.
- *incidents:* `{ incidents: [ { incidentType: "goal"|"card"|"substitution"|...,
  time, isHome, player?: { name }, playerIn?: { name }, playerOut?: { name },
  incidentClass?, text? } ] }`.
- *lineups:* `{ confirmed, home: { formation, players: [ { player: { name },
  jerseyNumber, substitute, position } ] }, away: { ... } }`.

## Decisions (approved)

1. **Scope:** header + statistics + incident timeline + lineups.
2. **All rows clickable.** Finished matches show full detail; scheduled matches
   open the page but show only the header + "Statistika dostupna nakon meča"
   (FootApi returns empty/204 for unplayed events — handled gracefully).
3. **Lazy fetch + cache.** Fetch the 3 FootApi calls only when a match detail is
   first requested. Cache finished-match details effectively permanently (long
   TTL); short TTL for non-finished. Bounds quota to matches actually opened.

## Backend Design

### Provider
`IFootballProvider` + `FootApiClient` gain three methods, each `GET
/api/match/{eventId}/{resource}` with paced calls and graceful empty handling
(204/404 → empty), returning parsed records:
- `GetMatchStatisticsAsync(int eventId)` → `IReadOnlyCollection<FootballStatItem>`
  (`Name`, `Home`, `Away`), flattened from the `ALL` period groups.
- `GetMatchIncidentsAsync(int eventId)` → `IReadOnlyCollection<FootballIncident>`
  (`Type`, `Minute`, `IsHome`, `PlayerName`, `PlayerInName`, `PlayerOutName`,
  `Detail`).
- `GetMatchLineupsAsync(int eventId)` → `FootballLineups`
  (`Confirmed`, `Home: FootballLineupTeam`, `Away`), each team with `Formation`
  and `Players: [ { Name, Number, IsSubstitute, Position } ]`. Null if absent.

### MatchDetailService
New `MatchDetailService` + `IMatchDetailService`, constructed with
`IRepository<Match>`, `IRepository<Team>`, `IFootballProvider`,
`IProviderRequestPacer`, `IMemoryCache`.

- `Task<MatchDetailResponse?> GetAsync(string matchId, CancellationToken)`:
  1. Load the `Match` by id; return `null` if not found (→ 404).
  2. Build the header from the match + the two `Team` docs (names, logos via the
     existing logo resolution; scores; round; season; status; date).
  3. If `Match.ProviderId` is null (un-ingested) → return header only (empty
     stats/incidents/lineups).
  4. Cache key `match-detail:{providerId}`. On miss, paced-fetch the 3 provider
     calls, map to DTO, cache (finished → 24h; otherwise → 2min).
  5. FootApi failure → throw `MatchDetailUnavailableException` (→ 502).

`MatchDetailResponse`: `{ MatchHeaderDto Header, IReadOnlyCollection<StatItemDto>
Statistics, IReadOnlyCollection<IncidentDto> Incidents, LineupsDto? Lineups }`.
- `MatchHeaderDto`: home/away `{ naziv, skracenica, logoUrl }`, `golDomacin`,
  `golGost`, `kolo`, `sezona`, `status`, `datum`.
- `StatItemDto(string Naziv, string Domacin, string Gost)`.
- `IncidentDto(string Tip, int Minut, bool Domacin, string Tekst)`
  (`Tekst` already humanized: goal scorer, card player, "X → Y" for subs).
- `LineupsDto(bool Potvrdjeno, LineupTeamDto Domacin, LineupTeamDto Gost)`;
  `LineupTeamDto(string Formacija, IReadOnlyCollection<LineupPlayerDto> Igraci)`;
  `LineupPlayerDto(string Ime, int Broj, bool Zamena, string Pozicija)`.

### Controller
`MatchDetailController` (public read):
- `GET /api/matches/{matchId}/detail` → 200 `MatchDetailResponse`; 404 when the
  match doesn't exist; 502 on `MatchDetailUnavailableException`.

DI: register `IMatchDetailService` + ensure `AddMemoryCache()` (already added).

## Frontend Design

### Types + API
- `types/api.ts`: add `providerId?: number | null` to `Match`; add
  `MatchDetail`, `StatItem`, `Incident`, `Lineups`, `LineupTeam`, `LineupPlayer`.
- `services/matchDetailApi.ts`: `get(matchId)` → `/api/matches/{matchId}/detail`.

### MatchRow → clickable
Wrap the row in a `react-router-dom` `Link` to `/mec/{match.id}` (whole row
clickable, hover affordance). Keep the existing layout.

### MatchDetail page (`/mec/:id`)
Route in `App.tsx`. Sections:
- **Header:** both teams (logo + name), big score (or scheduled time), round,
  season, date, status.
- **Statistics:** rows of `domacin — naziv — gost`, each with a proportional
  bar (home vs away). Skipped when empty.
- **Timeline (incidents):** chronological list by minute; goal / card / sub
  icons (lucide); home incidents left-aligned, away right-aligned. Skipped when
  empty.
- **Lineups:** two columns (home/away), formation label + starting XI, then
  substitutes; skipped when absent.
- States: loading, error (retry), and "Statistika dostupna nakon meča" for
  scheduled matches (empty stats/incidents/lineups but header present).

## Out of Scope (v1)
Head-to-head, odds, detailed player ratings/heatmaps, live auto-refresh,
pre-ingesting match details into Mongo.

## Testing Strategy (TDD, no MongoDB for unit tests)
- **Provider (`FootApiClient`):** parse representative statistics / incidents /
  lineups JSON into the records; empty/204 → empty result.
- **`MatchDetailService`:** with fakes — builds header from match + teams;
  returns header-only when `ProviderId` is null; caches per provider id
  (provider called once on repeat); maps stats/incidents/lineups; 404 path
  (match missing → null); provider failure → `MatchDetailUnavailableException`.
- **Controller:** 200 with body; 404 when service returns null; 502 on
  `MatchDetailUnavailableException`.
- **Frontend (vitest):** `MatchDetail` renders header + stats + timeline +
  lineups from a mocked api; shows the "after match" state when sections empty;
  `MatchRow` links to `/mec/{id}`.

## Acceptance Criteria
1. Clicking any match row on Results opens `/mec/{id}`.
2. A finished match shows header, statistics, incident timeline, and lineups.
3. A scheduled match shows the header and a "statistics after the match" notice.
4. Match details are fetched lazily and cached (repeat views hit cache, not
   FootApi).
5. All new backend + frontend tests pass; existing suites stay green.
