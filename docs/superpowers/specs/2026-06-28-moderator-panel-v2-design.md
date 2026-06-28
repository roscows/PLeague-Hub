# Moderator Panel v2 — Notices, Activity, Users — Design Spec

**Date:** 2026-06-28
**Status:** Approved (design), pending implementation plan

## Goal

Extend the `/moderacija` panel (currently only the reports queue) with three new
sections, all gated to moderator/administrator:

1. **Obavestenja (top):** a pinnable staff notice board (announcements/instructions
   to colleagues, forum-style) **plus** a compact feed of recent moderation
   activity (who muted/suspended whom).
2. **Korisnici:** search all users (username/email) + a separate staff list
   (admins/moderators), with **moderate** (mute/suspend) and **admin-only role
   change** (promote to moderator / demote to registered).
3. **Prijave:** the existing reports queue stays.

## Decisions (approved)

- Notices = staff-authored, pinnable messages (instructions/notes to colleagues).
- Notifications area also shows recent moderation actions (mute/suspend feed).
- Users section: search + staff list + **admin** role change (registrovani ↔ moderator).

## Reused

- `ModerationController` ([Authorize "moderator,administrator"]) hosts all new
  endpoints; role-change adds an admin-only guard inside the action.
- `IRepository<User>` (search/list/update role), `ModerationAction` collection
  (activity feed via `IRepository<ModerationAction>`), `IModerationService`
  (active state per user), `ModerationModal` (frontend moderate action).
- Open-generic `IRepository<T>` resolves for the new `StaffNoticeDocument` once
  registered in `MongoContext.GetCollection`.

## Models

`StaffNoticeDocument : BaseDocument` (collection `StaffNotices`):
- `Tekst` (string), `AutorId` (ObjectId), `Pinovano` (bool), `PinovanoAt`
  (DateTime?), `DatumKreiranja` (DateTime).
- Index: `idx_staffNotices_pin_datum` (Pinovano desc, DatumKreiranja desc).

## DTOs

- `StaffNoticeDto(string Id, string Tekst, string AutorUsername, bool Pinovano, DateTime DatumKreiranja)`
- `CreateStaffNoticeRequest(string Tekst)`
- `ModerationActivityDto(string Id, string Akcija, string? TipMere, string ModeratorUsername, string KorisnikUsername, string? Razlog, DateTime Datum)`
- `PanelUserDto(string Id, string Username, string Email, string Uloga, bool Aktivan, string? AktivnaMera)` — `AktivnaMera` = active moderation tip or null.
- `ChangeRoleRequest(string Uloga)` (`registrovani` | `moderator`)

## Endpoints (all under `ModerationController`)

**Notices:**
- `GET /api/moderation/notices` → list (pinned first, newest first), enriched author.
- `POST /api/moderation/notices` body `CreateStaffNoticeRequest` → 201; author = caller.
- `DELETE /api/moderation/notices/{id}` → 204/404.
- `POST /api/moderation/notices/{id}/pin` and `DELETE …/pin` → toggle pin (204/404).

**Activity:**
- `GET /api/moderation/activity?limit=20` → recent `ModerationAction` enriched
  (moderator + target usernames), newest first.

**Users:**
- `GET /api/moderation/users?q=&staffOnly=false` → `PanelUserDto[]`; `q` matches
  username/email (substring, case-insensitive, min 1 char or returns first N);
  `staffOnly=true` → only moderator/administrator. Capped (e.g. 30).
- `PUT /api/moderation/users/{id}/role` body `ChangeRoleRequest` — **admin only**
  (caller role check → 403 if not administrator); target must exist; cannot change
  own role; cannot change another administrator; new role ∈ {registrovani,
  moderator}. → 200 `PanelUserDto` / 400 / 403 / 404.

## Service

`StaffNoticeService` (+ interface): create/list(enriched)/delete/setPinned over
`IRepository<StaffNoticeDocument>` + `IRepository<User>`, `TimeProvider`.
Activity + users logic is simple enough to live in a `ModerationPanelService`
(+ interface): `GetActivityAsync(limit)`, `SearchUsersAsync(q, staffOnly)`,
`ChangeRoleAsync(callerId, targetId, role)` → result enum
(`Changed`/`Forbidden`/`NotFound`/`InvalidRole`/`SelfChange`/`TargetIsAdmin`),
using `IRepository<User>`, `IRepository<ModerationAction>`, `IModerationService`.

## Frontend

- `types/api.ts`: `StaffNotice`, `ModerationActivity`, `PanelUser` types + requests.
- `services/panelApi.ts`: notices (list/create/remove/togglePin), activity (list),
  users (search/changeRole). Reuse `moderationApi` for mute/suspend.
- `pages/ModerationPanel.tsx` restructured with three sections:
  - **Obavestenja** (top): notice composer (textarea + post), pinned-first list with
    pin/unpin + delete; below it a compact "Nedavna aktivnost" activity feed.
  - **Prijave** (existing reports queue).
  - **Korisnici**: search box → user rows (role badge, status, Moderisi button, and
    for admins a role dropdown/promote-demote); a "Tim (admini i moderatori)" list.
- Role-change UI shown only when `hasRole('administrator')`.

## Out of Scope (v1)
Notice editing (delete + repost instead), threaded notice replies, pagination,
notification read/unread state, audit of role changes beyond the role field.

## Testing (TDD)
- **StaffNoticeService:** create (author stamped); list pinned-first; delete;
  setPinned. **ModerationPanelService:** activity enriched newest-first;
  user search by username/email + staffOnly filter; role change happy +
  guards (non-admin Forbidden, self SelfChange, target-admin TargetIsAdmin,
  bad role InvalidRole, missing NotFound).
- **Endpoints (controller, fakes):** notices CRUD/pin statuses; activity 200;
  users 200; role change 200/400/403/404.
- **Frontend (vitest):** panel renders notices + activity + users sections from
  mocked api; posting a notice calls create; pin toggles; role change calls api.

## Acceptance Criteria
1. Mod/admin post pinnable staff notices visible to other staff; pinned show first.
2. The activity feed lists recent mute/suspend actions with both usernames.
3. Mod/admin search users and see staff list; admin can promote/demote
   (registrovani ↔ moderator) with guards; mute/suspend still works.
4. New backend + frontend tests pass; existing suites stay green.
