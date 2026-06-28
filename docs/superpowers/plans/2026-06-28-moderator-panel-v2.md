# Moderator Panel v2 — Implementation Plan

**Reference spec:** `docs/superpowers/specs/2026-06-28-moderator-panel-v2-design.md`

**Build/test:** backend `dotnet test … --filter <Name> -p:UseAppHost=false` (kill running api first; rerun once on SAC catastrophic-load). Frontend `npx vitest run <path>`. Commit production only.

---

## Task MP-1: StaffNotice collection
**Create:** `Models/StaffNoticeDocument.cs`.
**Modify:** `Configuration/MongoDbSettings.cs` (`StaffNoticesCollectionName="StaffNotices"`), `Data/MongoContext.cs` (collection + property + GetCollection case), `Data/MongoIndexInitializer.cs` (`idx_staffNotices_pin_datum`).
- [ ] Build (-o) → 0 errors. Commit: `Add StaffNotice collection`.

## Task MP-2: StaffNoticeService + notices endpoints
**Create:** `Responses/StaffNoticeDto.cs`, `Requests/CreateStaffNoticeRequest.cs`, `Services/StaffNoticeService.cs` (+ `IStaffNoticeService`).
**Modify:** `Program.cs` (register), `Controllers/ModerationController.cs` (inject; `GET/POST notices`, `DELETE notices/{id}`, `POST/DELETE notices/{id}/pin`).
**Test:** `StaffNoticeServiceTests.cs`, extend `ModerationPanelEndpointTests.cs`.
- [ ] Service: `CreateAsync(authorId, tekst)`, `GetAllAsync` (pinned desc, datum desc, enrich author username via `IRepository<User>`), `DeleteAsync(id)`, `SetPinnedAsync(id, pinned, now)`.
- [ ] Endpoints map: create→201, list→200, delete→204/404, pin→204/404. Author = `User.FindFirstValue(NameIdentifier)`.
- [ ] Run → PASS. Commit: `Add staff notices to moderator panel`.

## Task MP-3: ModerationPanelService activity + users + role; endpoints
**Create:** `Responses/ModerationActivityDto.cs`, `Responses/PanelUserDto.cs`, `Requests/ChangeRoleRequest.cs`, `Services/ModerationPanelService.cs` (+ `IModerationPanelService`, enum `RoleChangeResult`).
**Modify:** `Program.cs` (register), `Controllers/ModerationController.cs` (`GET activity`, `GET users`, `PUT users/{id}/role`).
**Test:** `ModerationPanelServiceTests.cs`, extend `ModerationPanelEndpointTests.cs`.
- [ ] Service deps `(IRepository<User>, IRepository<ModerationAction>, IModerationService, TimeProvider)`.
  - `GetActivityAsync(limit)`: actions newest-first, take limit, enrich moderator+target usernames from a users dict.
  - `SearchUsersAsync(q, staffOnly)`: filter username/email contains q (case-insensitive); staffOnly → role in {moderator,administrator}; cap 30; map to `PanelUserDto` (AktivnaMera via `IModerationService.GetActiveStateAsync` or from `user.AktivnaModeracija?.Tip`).
  - `ChangeRoleAsync(callerId, targetId, role)`: caller must be administrator (load caller) → else Forbidden; role ∈ {registrovani,moderator} else InvalidRole; target exists else NotFound; target==caller → SelfChange; target is administrator → TargetIsAdmin; else set `Uloga`, update → Changed.
- [ ] Endpoints: activity→200; users→200; role→200 PanelUserDto / 400(InvalidRole) / 403(Forbidden) / 404(NotFound) / 400(Self/TargetAdmin with message). Caller id from claims.
- [ ] Run → PASS; build. Commit: `Add activity feed and user management to moderator panel`.

## Task MP-4: Frontend types + panelApi
**Modify:** `types/api.ts` (`StaffNotice`, `ModerationActivity`, `PanelUser`, request types).
**Create:** `services/panelApi.ts` (notices: list/create/remove/togglePin; activity: list; users: search/changeRole).
**Test:** `services/panelApi.test.ts`.
- [ ] Run → PASS. Commit: `Add moderator panel v2 types and api`.

## Task MP-5: Panel sections — Obavestenja + Aktivnost
**Create:** `components/moderation/StaffNotices.tsx` (composer + pinned-first list, pin/unpin, delete), `components/moderation/ActivityFeed.tsx`.
**Modify:** `pages/ModerationPanel.tsx` (mount both at top, above Prijave).
**Test:** `components/moderation/StaffNotices.test.tsx`.
- [ ] Run → PASS; build. Commit: `Add staff notices and activity feed to panel UI`.

## Task MP-6: Panel section — Korisnici
**Create:** `components/moderation/UserManagement.tsx` (search box, user rows with role badge + status + Moderisi (ModerationModal) + admin role select; staff list).
**Modify:** `pages/ModerationPanel.tsx` (mount below Prijave), pass current user role.
**Test:** `components/moderation/UserManagement.test.tsx`.
- [ ] Search calls api; role change calls api (admin only control). Run → PASS; full `npx vitest run`; `npm run build`. Commit: `Add user management section to moderator panel`.

## Final Verification
- [ ] Backend full `dotnet test` green; build 0 errors. Frontend `npx vitest run` green; build ok.
- [ ] Live (api 5000): admin posts a notice + pins it; sees activity after a mute; searches users; promotes a registered user to moderator (and a non-admin gets 403). Push branch.
