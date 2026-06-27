# Player & Club Profile Pages — Design Spec

**Date:** 2026-06-27
**Status:** Approved (design), pending implementation plan

## Goal

Make players and clubs clickable across the app and open dedicated profile
pages (FlashScore-style):
- **Player** (`/igrac/:providerId`): MVP bio (photo, position, age, nationality,
  height, current club) + the per-season goals/assists/appearances we already
  store.
- **Club** (`/klub/:providerId`): club details (stadium, manager, founded,
  country) + standings position & recent form computed from stored matches +
  the full squad (roster).

## Decisions (approved)

- **Player scope:** MVP (bio + season stats we already have).
- **Club scope:** rich (details + form-from-DB + full roster).
- **Ingestion:** **lazy, persist-on-first-view** — identical to
  `MatchDetailService`. First open of a profile fetches from FootApi once and
  persists to Mongo forever; later views (and demos) read from the DB with no
  live call. No bulk admin sync.

## Context / Reused patterns

- `MatchDetailService` — persist-on-first-view (fetch once → store JSON → read
  from Mongo). Player/Club profile services follow this exactly.
- `LocalTeamLogoCache` + `GetTeamLogoAsync` — image bytes cached under
  `wwwroot/team-logos/{id}.ext`, served as `/team-logos/{id}.ext` and resolved
  by `resolveApiAssetUrl`. Player photos reuse this pattern under
  `wwwroot/player-photos/`.
- `StandingsService` — standings & form computed from the stored `Match`
  collection (no FootApi). Club form/position reuse this.
- `PlayerSeasonStat` (collection) — already holds per-season goals/assists/apps
  by player `ProviderId`. Player profile joins these.
- `Team` has `ProviderId`; `StandingRow.providerId` already exposed. Links use
  the FootApi provider id, mirroring `/mec/:id`.

## FootApi Endpoints (SofaScore-style — assumption, confirm live)

- Player bio: `GET /api/player/{playerId}` →
  `{ player: { id, name, position, height, dateOfBirthTimestamp,
  country: { name }, team: { id, name } } }`.
- Player photo: `GET /api/player/{playerId}/image` (binary, like team image).
- Club details: `GET /api/team/{teamId}` →
  `{ team: { id, name, venue: { stadium: { name } }, manager: { name },
  foundationDateTimestamp, country: { name } } }`.
- Club roster: `GET /api/team/{teamId}/players` →
  `{ players: [ { player: { id, name, position, jerseyNumber,
  country: { name } } } ] }`.

> **Assumption check (first implementation step):** with the API on a spare port
> and a temporary `DebugController`, hit `/api/player/{id}`, `/api/team/{id}`,
> `/api/team/{id}/players` and confirm the exact JSON keys; adjust the private
> parse records + tests if different. (Same lesson as `events`→`matches`.)

## Models (new collections, persist-on-first-view)

`PlayerProfileDocument : BaseDocument`
- `ProviderId` (int, indexed), `Ime`, `Pozicija`, `Drzava`, `Visina` (cm, int),
  `DatumRodjenja` (DateTime?), `KlubNaziv`, `KlubProviderId` (int), `FotoUrl`,
  `FetchedAt`.

`ClubProfileDocument : BaseDocument`
- `ProviderId` (int, indexed), `Stadion`, `Osnovan` (int), `Trener`, `Drzava`,
  `Roster` (array of `{ ProviderId, Ime, Pozicija, Broj, Drzava }`), `FetchedAt`.

Indexes: `idx_playerProfiles_providerId`, `idx_clubProfiles_providerId`.

## Provider additions (`IFootballProvider` + `FootApiClient` + fakes)

```csharp
Task<FootballPlayerProfile?> GetPlayerProfileAsync(int playerId, CancellationToken ct = default);
Task<FootballTeamLogo>      GetPlayerImageAsync(int playerId, CancellationToken ct = default);
Task<FootballTeamDetails?>  GetTeamDetailsAsync(int teamId, CancellationToken ct = default);
Task<IReadOnlyCollection<FootballRosterPlayer>> GetTeamPlayersAsync(int teamId, CancellationToken ct = default);
```
Records: `FootballPlayerProfile`, `FootballTeamDetails`, `FootballRosterPlayer`.
404/204 → null/empty. `GetPlayerImageAsync` mirrors `GetTeamLogoAsync`.

## Services (persist-on-first-view, like MatchDetailService)

`PlayerProfileService.GetAsync(int providerId)`:
1. Find `PlayerProfileDocument` by `ProviderId`; if missing → fetch bio + photo
   (paced), cache photo via `LocalPlayerPhotoCache`, persist the doc.
2. Join all `PlayerSeasonStat` rows for that `ProviderId` (ordered season desc).
3. Return `PlayerProfileDto(bio…, IReadOnlyCollection<PlayerSeasonLineDto>)`.
4. FootApi failure on first fetch → `ProfileUnavailableException` (502); unknown
   player with no data → 404.

`ClubProfileService.GetAsync(int providerId)`:
1. Find `ClubProfileDocument` by `ProviderId`; if missing → fetch details +
   roster (paced), persist.
2. Resolve `Team` by `ProviderId` (naziv/logo); compute current-season position
   (newest season from `StandingsService`) + last-5 form and recent matches from
   stored `Match` rows for that team.
3. Return `ClubProfileDto(details…, position, form, recentMatches, roster)`.

## Link data (so teams/players are clickable everywhere)

- `PlayerSeasonStatDocument` + `PlayerStatDto` + `PlayerStat` (FE): add
  `TeamProviderId` (sync already has `player.TeamId`; re-ingest or lazy-fill).
- `MatchTeamDto` + `MatchTeamInfo` (FE): add `ProviderId` (from `Team.ProviderId`)
  so MatchDetail header teams link to `/klub/:id`.
- Results rows already carry team identity; expose provider id if missing.

## Read APIs

- `GET /api/players/{providerId}` → `PlayerProfileDto`.
- `GET /api/clubs/{providerId}` → `ClubProfileDto`.

## Frontend

- `types/api.ts`: `PlayerProfile`, `ClubProfile`, roster/line/form types; extend
  `PlayerStat`/`MatchTeamInfo` with provider ids.
- `services/playersApi.ts`, `services/clubsApi.ts`.
- `pages/PlayerProfile.tsx` (`/igrac/:id`), `pages/ClubProfile.tsx` (`/klub/:id`)
  — header (photo/logo, name, key facts), season-stats table (player) / details +
  form + roster (club), loading / not-found / error states.
- Routes in `App.tsx`; nav stays the same (profiles are drill-downs).
- Make clickable: Stats (player name → player, team → club), TablePage (team →
  club), MatchDetail header teams → club, Results rows teams → club. Lineup
  players stay non-clickable (no id available) — out of scope.

## Out of Scope (v1)
Career history across clubs, market value, per-90 metrics, clickable lineup
players (no id), club fixtures calendar, head-to-head, bulk warm-all ingestion.

## Testing Strategy (TDD, no MongoDB for unit tests)
- **Provider:** parse representative player/team/roster JSON; 404 → null/empty.
- **PlayerProfileService:** first view fetches + persists + joins season stats;
  second view reads from repo (no provider call); FootApi failure → exception.
- **ClubProfileService:** first view persists details+roster; computes
  position/form from stored matches; second view no provider call.
- **Controllers:** return DTOs; 404 unknown; 502 on ProfileUnavailable.
- **Frontend (vitest):** profile pages render from mocked api (bio + stats /
  details + roster); not-found state; links carry the right `providerId`.

## Acceptance Criteria
1. Clicking a player (Statistike) opens `/igrac/:id` with bio + per-season stats.
2. Clicking a club (Tabela, Statistike team, MatchDetail teams, Results) opens
   `/klub/:id` with details, standings position, recent form, and full roster.
3. First open fetches from FootApi and persists; subsequent opens read from Mongo
   (verify: stop network / second call makes no provider request).
4. All new backend + frontend tests pass; existing suites stay green.
