# Player Statistics (Top Scorers + Assisters) — Design Spec

**Date:** 2026-06-26
**Status:** Approved (design), pending implementation plan

## Goal

Replace the Statistike tab's seeded fake players with real top scorers and
assisters per season, ingested from FootApi into MongoDB so the tab is
DB-backed and presentable offline.

## Context / Current State

- The Statistike tab (`frontend/src/pages/Stats.tsx`) shows a player table
  (#, player, team, position, goals, assists, rating) driven by
  `playersApi.list()` over the `Players` collection — currently **6 seeded fake
  players** (Saka, Salah, …). No season concept.
- The session already established the admin-sync + DB-persist patterns
  (`MatchSyncService`, match detail persistence) and the seasons-from-matches
  list (`/api/standings/seasons`). Matches/teams/standings are all DB-backed.
- The `Players` collection is also used by global search; it must **not** be
  repurposed. Player season stats get their own collection.

## Decision (approved)

Top **scorers + assisters per season**, one ranked table with a Gol/Ast sort
toggle and a season selector. DB-backed (FootApi on admin sync, read from Mongo).

## FootApi Endpoint (path confirmed from playground)

`GET /api/tournament/{tournamentId}/season/{seasonId}/best-players`

- **To verify during implementation (debug call; quota is available):**
  1. the `tournamentId` for the Premier League on this endpoint — try `17`
     (uniqueTournament, as standings use) first, then the season-tournament id
     `1` (as matches use) if `17` returns empty;
  2. the response shape — expected SofaScore-style grouping such as
     `{ topPlayers: { goals: [ { player: { id, name }, team: { id, name },
     statistics: { goals } } ], assists: [ … ] , rating: [ … ] } }`. Adjust the
     private parse records once the live shape is confirmed.

## Model

New collection `PlayerSeasonStat` (`PlayerSeasonStatDocument : BaseDocument`):
- `Sezona` (string, normalized e.g. `2024/25`)
- `ProviderId` (int, FootApi player id)
- `Ime` (full name)
- `TeamNaziv` (string), `TeamLogoUrl` (string) — denormalized (player's club may
  differ per season; logo resolved from the `Team` doc by team provider id when
  available, else empty)
- `Golovi` (int), `Asistencije` (int), `Odigrano` (int, appearances; 0 if absent)

Idempotency by `(Sezona, ProviderId)` (service find-then-upsert; non-unique
index `idx_playerSeasonStats_sezona`).

## Provider

`IFootballProvider` + `FootApiClient` gain:
```csharp
Task<IReadOnlyCollection<FootballPlayerStat>> GetBestPlayersAsync(
    int tournamentId, int seasonId, CancellationToken ct = default);
```
- `FootballPlayerStat(int ProviderId, string Name, int TeamId, string TeamName,
  int Goals, int Assists, int Appearances)` — merged from the `goals` and
  `assists` groups of the `best-players` response (union by player id; a player
  in only one group gets 0 for the other).
- 204/404 → empty.

## PlayerStatsSyncService (admin)

New `PlayerStatsSyncService` + `IPlayerStatsSyncService`, constructed with
`IFootballProvider`, `IRepository<Team>`, `IRepository<PlayerSeasonStatDocument>`,
`IProviderRequestPacer`.

- `SyncSeasonAsync(int seasonId, CancellationToken)`:
  1. Resolve the season label via `provider.GetSeasonsAsync(17)` (find matching
     id → `NormalizeSeasonLabel(year)`), throw `PlayerStatsSyncException` if not
     found.
  2. Paced `GetBestPlayersAsync(PremierLeagueTournamentId, seasonId)`.
  3. For each player: resolve `TeamNaziv`/`TeamLogoUrl` from the `Team` doc by
     team provider id (denormalized; empty if no match).
  4. Upsert by `(label, providerId)` into `PlayerSeasonStat`.
  5. Return `PlayerStatsSyncResponse(int Total, int Created, int Updated)`.
- Provider failures wrapped in `PlayerStatsSyncException` → 502.
- `PremierLeagueTournamentId` constant = the verified id from the endpoint check.

Endpoint on `IntegrationsController` (admin-only):
`POST /api/integrations/football/sync/player-stats?seasonId={id}` → counts; 502
on `PlayerStatsSyncException`.

## Read API

`PlayerStatsController` (public read):
- `GET /api/player-stats?season=2024/25` → `IReadOnlyCollection<PlayerStatDto>`
  (`Position, ProviderId, Ime, TeamNaziv, TeamLogoUrl, Golovi, Asistencije,
  Odigrano`) ordered by goals desc, then assists desc; `Position` = rank.
  Empty list for a season with no ingested stats.
- Seasons for the dropdown reuse the existing `/api/standings/seasons`.

## Frontend (Statistike tab)

- `types/api.ts`: `PlayerStat` interface; `services/playerStatsApi.ts`
  (`get(season)` and reuse `standingsApi.getSeasons()`).
- `pages/Stats.tsx` rework:
  - Season `<select>` (default newest from the seasons list).
  - Sort toggle **Gol / Ast** (client-side reorder of the fetched list).
  - Table: `# · Igrač · Tim (logo) · Gol · Ast`. Keep the text search as a
    client-side filter over the season's list. Drop the team filter and the
    fake rating column.
  - Loading / empty ("Nema statistike za izabranu sezonu") / error states.

## Out of Scope (v1)
Ratings/clean-sheets as separate tabs, player profile pages, per-90 metrics,
auto-refresh, pre-ingesting all seasons in one run (admin syncs per season,
within the daily quota).

## Testing Strategy (TDD, no MongoDB for unit tests)
- **Provider:** parses a representative `best-players` JSON (goals + assists
  groups) and merges by player id; empty on 204.
- **PlayerStatsSyncService:** resolves season label; maps + denormalizes team;
  upserts idempotently (re-run updates, no duplicate); provider failure →
  `PlayerStatsSyncException`.
- **PlayerStatsController:** returns ordered DTOs for a season; empty for unknown
  season.
- **Frontend (vitest):** Stats renders rows for the default season from a mocked
  api; the Gol/Ast toggle reorders; search filters; empty state shows.

## Acceptance Criteria
1. `POST /sync/player-stats?seasonId=…` ingests that season's top scorers +
   assisters into Mongo (idempotent).
2. Statistike tab shows real players for the selected season, ordered by goals,
   with an Ast sort toggle and working search.
3. Seasons with no ingested stats show an empty-state message.
4. All new backend + frontend tests pass; existing suites stay green.
