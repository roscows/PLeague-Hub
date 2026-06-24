# Match Ingestion (FootApi → Mongo) — Design Spec

**Date:** 2026-06-24
**Status:** Approved (design), pending implementation plan
**Feature:** A of 2 (A = ingestion; B = match detail + stats page, separate spec)

## Goal

An admin-triggered sync that pulls Premier League matches from FootApi into
MongoDB, idempotently, for a single season or all seasons. Each stored match
carries the FootApi event id so a later feature (match detail/stats) can fetch
per-match data.

## Context / Current State

- `Match` (`backend/PLeagueHub.Api/Models/Match.cs`) has no provider/event id.
  `DomacinId`/`GostId` are Mongo `ObjectId` references to `Team` docs; `Sezona`
  is a string, `Kolo` an int, `Status` a string (`zavrsena`/`zakazana`/`uzivo`),
  `GolDomacin`/`GolGost` nullable ints, `Datum` a UTC `DateTime`.
- Teams already carry `ProviderId` (FootApi team id), set by `TeamSyncService`.
  The current Team collection holds only the 20 current PL teams.
- `IFootballProvider`/`FootApiClient` already implement `SearchAsync`,
  `GetTeamStandingsAsync`, `GetSeasonsAsync(tournamentId)`, `GetTeamLogoAsync`.
  `GetSeasonsAsync(17)` returns 35 seasons (verified live).
- `IntegrationsController` (admin-only, `api/integrations/football`) already
  exposes `sync/teams` and `sync/team-logos` following a `*SyncService` +
  `*SyncException` → `502` pattern. `IProviderRequestPacer` paces FootApi calls.
- The FootApi key works and the running API reaches FootApi successfully.

## Decisions (approved)

1. **Scope:** all 35 seasons (user accepted the RapidAPI quota cost; ~1300+
   paginated calls, long-running). The endpoint also supports a single season.
2. **Trigger:** admin endpoint, manually invoked, paced, long-running.
3. **Teams:** upsert each team encountered by `ProviderId` (create a Team doc if
   missing) so historical/relegated teams resolve. Also improves crest coverage.
4. **Season label:** normalize FootApi year (`26/27`) to the existing
   `2026/27` format so ingested matches align with the season selector and any
   seeded matches.

## FootApi Contract (assumption — verify during implementation)

This is a SofaScore mirror; exact field names verified in implementation, same
as we did for standings/seasons.

- Fixtures per season, paginated:
  - `GET /api/tournament/{tournamentId}/season/{seasonId}/events/last/{page}` — finished, newest first.
  - `GET /api/tournament/{tournamentId}/season/{seasonId}/events/next/{page}` — upcoming.
  - Loop pages from 0 until a page returns no events.
- Event shape (fields used): `id` (event id), `roundInfo.round`,
  `homeTeam { id, name, nameCode }`, `awayTeam { id, name, nameCode }`,
  `homeScore.current`, `awayScore.current`, `status.type`
  (`finished` | `inprogress` | `notstarted`), `startTimestamp` (unix seconds).

If field names differ, adjust the private parse records (the public service
contract stays stable). If pagination differs, fall back to per-round fetch
`…/events/round/{round}` for rounds 1..38.

## Model Changes

`Match` gains:
```csharp
[BsonElement("provider_id")]
[BsonIgnoreIfNull]
public int? ProviderId { get; set; }
```
Nullable + `BsonIgnoreIfNull` so existing seeded matches (no provider id) don't
store the field at all. A new **non-unique** Mongo index on `provider_id`
(added in `MongoIndexInitializer`) speeds upsert lookups. Idempotency is enforced
by the service (find-by-`ProviderId` then update-or-create), not a DB unique
constraint — this avoids null-key collisions with the seeded matches.

## Provider Changes

`IFootballProvider` + `FootApiClient` gain:
```csharp
Task<IReadOnlyCollection<FootballEvent>> GetSeasonEventsAsync(
    int tournamentId, int seasonId, CancellationToken cancellationToken = default);
```
- `FootballEvent` record: `EventId`, `Round`, `HomeTeamId`, `HomeTeamName`,
  `HomeTeamCode`, `AwayTeamId`, `AwayTeamName`, `AwayTeamCode`, `HomeScore`
  (int?), `AwayScore` (int?), `StatusType` (string), `StartTimestamp` (long).
- Implementation pages `events/last` then `events/next` until empty, mapping
  rows. Existing fake providers in tests get the new method.

## MatchSyncService

New `MatchSyncService` (mirrors `TeamSyncService`), constructed with
`IFootballProvider`, `IRepository<Team>`, `IRepository<Match>`,
`IProviderRequestPacer`.

- `Task<MatchSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken)`:
  1. Paced fetch of all events for the season via the provider.
  2. For each event: resolve/create home & away Team by `ProviderId`
     (`EnsureTeamAsync` — find by providerId, else create with name + nameCode);
     collect their `Id`s.
  3. Map event → `Match` (provider id, kolo, normalized season, datum from
     timestamp, scores, mapped status).
  4. Upsert by `ProviderId`: update existing match in place, else create.
  5. Return counts: `created`, `updated`, `teamsCreated`, `total`.
- `Task<MatchSyncResponse> SyncAllSeasonsAsync(CancellationToken)`: fetch
  seasons via provider, fold `SyncSeasonAsync` over each, aggregate counts.
- Status map: `finished`→`zavrsena`, `inprogress`→`uzivo`, else `zakazana`.
- Season normalization (`NormalizeSeasonLabel`): a `YY/YY` label → `YYYY/YY`
  with century inference — start years `90`–`99` map to `19xx`, `00`–`89` to
  `20xx` (e.g., `26/27`→`2026/27`, `99/00`→`1999/00`). A label already in
  `YYYY/YY`/`YYYY/YYYY` form is passed through unchanged.
- Provider failures wrapped in `MatchSyncException` (→ `502`), matching the
  team-sync pattern.

`MatchSyncResponse` record: `(int Total, int Created, int Updated, int TeamsCreated)`.

## Controller

Extend `IntegrationsController` (admin-only):
- `POST /api/integrations/football/sync/matches?seasonId={id}` → single season.
- `POST /api/integrations/football/sync/matches?all=true` → all seasons.
- Validate: exactly one of `seasonId`/`all`. `MatchSyncException` → `502`.

DI: register `MatchSyncService` (scoped) in `Program.cs`.

## Out of Scope (Feature B / later)

- Match statistics, incidents (goals/cards), lineups, and the clickable detail
  page (separate spec B, which depends on `Match.ProviderId`).
- Live/auto refresh. Logo download for newly-created historical teams (the
  existing `sync/team-logos` + standings lazy-download already cover crests).
- Re-sync optimization (skipping immutable finished seasons) — v1 re-syncs all;
  upserts make it safe to re-run.

## Testing Strategy (TDD, no MongoDB for unit tests)

- **Provider (`FootApiClient`):** parses a representative `events/last` JSON
  payload into `FootballEvent`s (ids, round, teams, scores, status, timestamp);
  pagination stops on empty page.
- **`MatchSyncService`:** with fake provider + fake repos —
  - maps an event to a `Match` (status, season normalization, datum, scores);
  - creates a missing team and links both team ids (`teamsCreated` counted);
  - reuses an existing team by `ProviderId` (not recreated);
  - upserts idempotently (re-running the same event updates, no duplicate);
  - `SyncAllSeasonsAsync` aggregates counts across seasons;
  - provider failure → `MatchSyncException`.
- **Controller:** `sync/matches` returns counts; rejects when neither/both of
  `seasonId`/`all` provided; `502` on `MatchSyncException`.

## Acceptance Criteria

1. `POST /sync/matches?seasonId=…` ingests that season's matches into Mongo with
   provider ids, correct team links, scores, status, round, normalized season.
2. `POST /sync/matches?all=true` ingests all seasons.
3. Re-running does not duplicate matches (idempotent by provider id).
4. Historical teams not previously in the DB are created.
5. All new backend unit tests pass; existing suites remain green.
