# Player & Club Profile Pages — Implementation Plan

**Goal:** Clickable players/clubs opening FlashScore-style profile pages. Player
= MVP bio + per-season stats; Club = details + form-from-DB + roster. Ingestion
is **lazy persist-on-first-view** (like `MatchDetailService`).

**Reference spec:** `docs/superpowers/specs/2026-06-27-player-club-profiles-design.md`

**Tech:** .NET 10 (MongoDB.Driver, `IProviderRequestPacer`, xUnit), React 19 + TS
+ Tailwind + react-router-dom, vitest.

**Build/test:** backend `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter <Name> -p:UseAppHost=false`; frontend `cd frontend && npm test -- <Name>`. Kill any running `PLeagueHub.Api.dll` before a build (DLL lock).

**Conventions:** tests under `backend/PLeagueHub.Api.Tests/` and `frontend/src/**/*.test.*` are git-ignored — commit production only. FootApi player/team/roster shapes are an **assumption** (SofaScore-style); Task 0 confirms them live before relying on parses.

---

## Task 0: Assumption check (live shape)

- [ ] On a spare port (5050), re-add a temporary `DebugController` (`/api/debug/foot?path=`), build, run. Probe `/api/player/{id}` (e.g. Salah `159665`), `/api/team/{id}` (a PL club), `/api/team/{id}/players`. Record the exact JSON keys for: player position/height/dateOfBirthTimestamp/country/team; team venue.stadium.name/manager.name/foundationDateTimestamp/country; roster player jerseyNumber/position/country.
- [ ] Adjust the private parse records in Tasks 1 & 5 (+ their test JSON) to match. Remove `DebugController`. Do **not** commit `DebugController`.

---

## Task 1: Provider — player bio + photo

**Create:** `Services/Football/FootballPlayerProfile.cs` (`record FootballPlayerProfile(int ProviderId, string Name, string Position, int Height, DateTime? DateOfBirth, string Country, int TeamId, string TeamName)`).
**Modify:** `IFootballProvider.cs`, `FootApiClient.cs`, the **6** fakes (`TeamSyncServiceTests`, `TeamLogoSyncServiceTests`, `IntegrationEndpointsTests`, `MatchSyncServiceTests`, `MatchDetailServiceTests`, `PlayerStatsSyncServiceTests`).
**Test:** `PlayerProfileProviderTests.cs`.

- [ ] Add `GetPlayerProfileAsync(int playerId, ct)` (parse `/api/player/{id}`, 404→null) and `GetPlayerImageAsync(int playerId, ct)` (clone of `GetTeamLogoAsync` against `/api/player/{id}/image`) to interface + client + private records.
- [ ] Add both stubs to all 6 fakes (`=> Task.FromResult<…>(null!/[])`).
- [ ] Test: parses representative player JSON; 404 → null. Run → PASS.
- [ ] Commit: `Add FootApi player profile + image provider calls`.

---

## Task 2: PlayerProfile collection + photo cache

**Create:** `Models/PlayerProfileDocument.cs`, `Services/Football/IPlayerPhotoCache.cs`, `Services/Football/LocalPlayerPhotoCache.cs` (clone of `LocalTeamLogoCache`, dir `player-photos`).
**Modify:** `Configuration/MongoDbSettings.cs` (`PlayerProfilesCollectionName = "PlayerProfiles"`), `Data/MongoContext.cs` (collection + property + `GetCollection` case), `Data/MongoIndexInitializer.cs` (`idx_playerProfiles_providerId`), `Program.cs` (register `IPlayerPhotoCache`).

- [ ] `PlayerProfileDocument`: fields per spec (`ProviderId, Ime, Pozicija, Drzava, Visina, DatumRodjenja, KlubNaziv, KlubProviderId, FotoUrl, FetchedAt`).
- [ ] Build → 0 errors. Commit: `Add PlayerProfile collection and photo cache`.

---

## Task 3: PlayerProfileService (persist-on-first-view) + DTO

**Create:** `Responses/PlayerProfileDto.cs` (`record PlayerProfileDto(int ProviderId, string Ime, string Pozicija, string Drzava, int Visina, int? Godine, string KlubNaziv, int KlubProviderId, string FotoUrl, IReadOnlyCollection<PlayerSeasonLineDto> Sezone)`; `PlayerSeasonLineDto(string Sezona, string TeamNaziv, int TeamProviderId, int Golovi, int Asistencije, int Odigrano)`), `Services/Football/ProfileUnavailableException.cs`, `Services/Football/PlayerProfileService.cs` (+ `IPlayerProfileService`).
**Test:** `PlayerProfileServiceTests.cs`.

- [ ] Service ctor `(IRepository<PlayerProfileDocument>, IRepository<PlayerSeasonStatDocument>, IFootballProvider, IPlayerPhotoCache, IProviderRequestPacer)`. `GetAsync(int providerId)`:
  - find doc by `ProviderId`; if missing → paced `GetPlayerProfileAsync` (null → return null/404), paced `GetPlayerImageAsync` → `cache.SaveAsync` → `FotoUrl = cache.GetPublicUrl(id)`, persist doc; provider/json/io failure → `ProfileUnavailableException`.
  - join `PlayerSeasonStat` where `ProviderId == id`, season desc → `Sezone`. Compute `Godine` from `DatumRodjenja`.
- [ ] Tests: first view fetches+persists+joins; second view makes **no** provider call (fake counts calls); failure → exception. Run → PASS.
- [ ] Commit: `Add player profile service with lazy persistence`.

---

## Task 4: PlayersController + DI

**Create:** `Controllers/PlayersController.cs` (`GET /api/players/{providerId:int}` → 200 dto / 404 / 502 on `ProfileUnavailableException`).
**Modify:** `Program.cs` (register `IPlayerProfileService`).
**Test:** `PlayerProfileReadTests.cs` (200 with dto; 404 unknown; 502 on exception).

- [ ] Run → PASS; build → 0 errors. Commit: `Expose player profile read endpoint`.

---

## Task 5: Provider — team details + roster

**Create:** `Services/Football/FootballTeamDetails.cs` (`record …(int ProviderId, string Name, string Stadium, int Founded, string Manager, string Country)`), `Services/Football/FootballRosterPlayer.cs` (`record …(int ProviderId, string Name, string Position, int Number, string Country)`).
**Modify:** `IFootballProvider.cs`, `FootApiClient.cs`, the **6** fakes.
**Test:** `TeamProfileProviderTests.cs`.

- [ ] Add `GetTeamDetailsAsync` (`/api/team/{id}`, 404→null) + `GetTeamPlayersAsync` (`/api/team/{id}/players`, 404/204→[]) + private records; stub in 6 fakes.
- [ ] Tests: parse details + roster JSON; 404 handling. Run → PASS.
- [ ] Commit: `Add FootApi team details + roster provider calls`.

---

## Task 6: ClubProfile collection

**Create:** `Models/ClubProfileDocument.cs` (+ nested `ClubRosterEntry`).
**Modify:** `MongoDbSettings.cs` (`ClubProfilesCollectionName = "ClubProfiles"`), `MongoContext.cs`, `MongoIndexInitializer.cs` (`idx_clubProfiles_providerId`).

- [ ] Build → 0 errors. Commit: `Add ClubProfile collection`.

---

## Task 7: ClubProfileService (persist-on-first-view) + DTO

**Create:** `Responses/ClubProfileDto.cs` (`record …(int ProviderId, string Naziv, string LogoUrl, string Stadion, int Osnovan, string Trener, string Drzava, int Pozicija, IReadOnlyCollection<string> Forma, IReadOnlyCollection<ClubMatchDto> PoslednjiMecevi, IReadOnlyCollection<ClubRosterDto> Roster)`; `ClubMatchDto`, `ClubRosterDto(int ProviderId, string Ime, string Pozicija, int Broj, string Drzava)`), `Services/Football/ClubProfileService.cs` (+ interface).
**Test:** `ClubProfileServiceTests.cs`.

- [ ] Ctor `(IRepository<ClubProfileDocument>, IRepository<Team>, IRepository<Match>, IFootballProvider, IProviderRequestPacer)`. `GetAsync(int providerId)`:
  - resolve `Team` by `ProviderId` (null → 404).
  - find `ClubProfileDocument`; if missing → paced details + roster → persist (`ProfileUnavailableException` on failure).
  - position: newest season via matches → reuse standings tally logic (or inject `IStandingsService`); form = last-5 finished matches for `Team.Id` → `["W","D","L",…]`; `PoslednjiMecevi` last 5.
- [ ] Tests: first view persists details+roster; computes form/position from stored matches; second view no provider call. Run → PASS.
- [ ] Commit: `Add club profile service with lazy persistence`.

---

## Task 8: ClubsController + DI

**Create:** `Controllers/ClubsController.cs` (`GET /api/clubs/{providerId:int}` → 200/404/502).
**Modify:** `Program.cs` (register `IClubProfileService`).
**Test:** `ClubProfileReadTests.cs`.

- [ ] Run → PASS; build → 0 errors. Commit: `Expose club profile read endpoint`.

---

## Task 9: Link data — provider ids on stats + match teams

**Modify:** `Models/PlayerSeasonStatDocument.cs` (+`TeamProviderId`), `Services/Football/PlayerStatsSyncService.cs` (set `TeamProviderId = player.TeamId`), `Responses/PlayerStatDto.cs` (+`TeamProviderId`), `Controllers/PlayerStatsController.cs` (map it); `Responses/MatchHeaderDto`/`MatchTeamDto` (+`ProviderId`), `MatchDetailService.ToTeamDto` (pass `team?.ProviderId ?? 0`).
**Test:** extend `PlayerStatsReadTests` / `MatchDetailServiceTests` assertions.

- [ ] Run affected backend tests → PASS. Re-ingest player-stats live (4 seasons) so `TeamProviderId` is populated (or note lazy-fill). Commit: `Add provider ids to player stats and match teams`.

---

## Task 10: Frontend types + api clients

**Modify:** `types/api.ts` (`PlayerProfile`, `PlayerSeasonLine`, `ClubProfile`, `ClubMatch`, `ClubRoster`; add `teamProviderId` to `PlayerStat`, `providerId` to `MatchTeamInfo`).
**Create:** `services/playersApi.ts` (`getProfile(id)`→`/api/players/{id}`), `services/clubsApi.ts` (`getProfile(id)`→`/api/clubs/{id}`).
**Test:** `playersApi.test.ts`, `clubsApi.test.ts`.

- [ ] Run → PASS. Commit: `Add player/club profile types and api clients`.

---

## Task 11: PlayerProfile page + route

**Create:** `pages/PlayerProfile.tsx`, `pages/PlayerProfile.test.tsx`.
**Modify:** `App.tsx` (`<Route path="igrac/:id" …>`).

- [ ] Header (photo via `TeamLogo`-style `<img>`/initials, name, position, club link, age/nationality/height chips) + season-stats table (`Sezona · Tim · Gol · Ast · Nastupi`). Loading / not-found / error. Club name links to `/klub/:klubProviderId`.
- [ ] Tests: renders bio + season rows from mocked api; not-found state. Run → PASS. Commit: `Add player profile page`.

---

## Task 12: ClubProfile page + route

**Create:** `pages/ClubProfile.tsx`, `pages/ClubProfile.test.tsx`.
**Modify:** `App.tsx` (`<Route path="klub/:id" …>`).

- [ ] Header (logo, name, position, stadium/manager/founded/country chips), form pills (last 5), recent matches list, roster grid (player → `/igrac/:providerId`). Loading / not-found / error.
- [ ] Tests: renders details + roster + form from mocked api; not-found. Run → PASS. Commit: `Add club profile page`.

---

## Task 13: Wire clickable links

**Modify:** `pages/Stats.tsx` (player name → `/igrac/:providerId`, team cell → `/klub/:teamProviderId`), `pages/TablePage.tsx` (team row → `/klub/:providerId`), `pages/MatchDetail.tsx` (header teams → `/klub/:providerId`), `components/MatchRow.tsx`/`pages/Results.tsx` (team → `/klub/:providerId` if provider id available).
**Test:** extend `Stats.test.tsx` (link href) + existing page tests.

- [ ] Use `<Link>`; keep row click for matches intact (stop propagation on inner links where needed). Run frontend suite → PASS; `npm run build`. Commit: `Make players and clubs clickable across the app`.

---

## Final Verification (verification-before-completion)

- [ ] Backend: kill running api, `dotnet test … --filter "Profile" -p:UseAppHost=false` + full suite → green; build → 0 errors.
- [ ] Frontend: `npm test` → green; `npm run build` → succeeds.
- [ ] **Live, on spare port:** open a player (Salah) → `/igrac/159665` shows bio + seasons; open its club → `/klub/:id` shows details + form + roster; **second** open of each makes no FootApi call (check logs / it works offline after). Confirm Mongo has `PlayerProfiles` + `ClubProfiles` docs.
- [ ] finishing-a-development-branch: tests green → push branch.
