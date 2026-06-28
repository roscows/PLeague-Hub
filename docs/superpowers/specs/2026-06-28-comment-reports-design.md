# Comment Reports + Moderator Panel — Design Spec

**Date:** 2026-06-28
**Status:** Approved (design), pending implementation plan

## Goal

Let registered users **report** a forum comment; moderators/administrators see
pending reports in a protected **Moderacija** panel and act on them. This adds a
clean two-user-type interaction (registered → moderator) on top of existing
moderation. The panel is built as an extensible shell — reports is its first
section; future sections (user search, admin/mod list, notifications) plug in
later.

## Decisions (approved)

- **Reason:** category (`spam`, `uvrede`, `offtopic`, `ostalo`) + optional free-text
  explanation.
- **Moderator actions on a report:** delete the comment, dismiss the report,
  and moderate the comment's author (mute/suspend) — reusing existing moderation.
- **Panel:** a new protected route `/moderacija`, visible only to
  moderator/administrator (nav link hidden for others). Reports section first.

## Context / Reused

- `Comment` is soft-deleted (`Obrisan`); `IModerationRepository` exposes
  `GetCommentAsync`, `GetUserAsync`, `SetCommentDeletedAsync`.
- `ModerationController` ([Authorize "moderator,administrator"]) hosts existing
  user/post/comment moderation; the report queue + resolve endpoints join it.
- Comment-create / write endpoints on `ForumController` are `[Authorize]`
  (registered); the report-create endpoint joins it.
- Frontend: `ModerationModal` (mute/suspend a user) reused for "moderate author";
  `moderationApi`, `ProtectedRoute`, `useAuth().hasRole`, the `navItems` list in
  `Layout`.

## Model

`CommentReportDocument : BaseDocument` (collection `CommentReports`):
- `KomentarId` (ObjectId ref Comment), `PostId` (ObjectId, for deep-link)
- `PrijavioId` (ObjectId, reporter)
- `Kategorija` (string enum), `Opis` (string, optional)
- `Status` (`na_cekanju` | `reseno` | `odbaceno`)
- `DatumPrijave` (DateTime)
- `ResioId` (ObjectId?, resolver), `ResenoAt` (DateTime?), `Ishod`
  (`komentar_obrisan` | `odbaceno`, null while pending)

Index: `idx_commentReports_status_datum` (Status asc, DatumPrijave desc) and
`idx_commentReports_komentar_prijavio` (KomentarId, PrijavioId) to dedupe.

## Service — `CommentReportService` (+ `ICommentReportService`)

Deps: `IRepository<CommentReportDocument>`, `IModerationRepository`,
`IRepository<User>` (reporter/author usernames), `TimeProvider`.

- `CreateAsync(commentId, reporterId, kategorija, opis)` → result enum:
  - validate category ∈ allowed → else `InvalidCategory`
  - comment missing or deleted → `CommentNotFound`
  - reporter == comment.AutorId → `CannotReportOwn`
  - existing **pending** report by this reporter for this comment → `DuplicatePending`
  - else create `na_cekanju` → `Created`
- `GetPendingAsync()` → `IReadOnlyCollection<CommentReportDto>` enriched
  (comment text, author id+username, reporter username), `na_cekanju` only,
  newest first; skips reports whose comment is already deleted/missing.
- `ResolveAsync(reportId, moderatorId, akcija)` → result enum
  (`Resolved`/`NotFound`/`InvalidAction`):
  - `obrisi` → `SetCommentDeletedAsync(komentarId)`, mark **all** pending reports
    of that comment `reseno` + `Ishod=komentar_obrisan`
  - `odbaci` → mark this report `odbaceno` + `Ishod=odbaceno`
  - both stamp `ResioId`, `ResenoAt`

## DTOs

- `CreateCommentReportRequest { string Kategorija; string? Opis }`
- `ResolveReportRequest { string Akcija }` (`obrisi`|`odbaci`)
- `CommentReportDto { string Id; string KomentarId; string PostId;
  string KomentarTekst; string AutorId; string AutorUsername;
  string PrijavioUsername; string Kategorija; string Opis;
  DateTime DatumPrijave }`

## Endpoints

- `POST /api/forum/comments/{commentId}/report` (ForumController, `[Authorize]`)
  body `CreateCommentReportRequest` → 201 Created / 200 (duplicate, idempotent) /
  400 (own/invalid category) / 404 (comment) / 401.
- `GET /api/moderation/reports` (ModerationController) → 200 list.
- `POST /api/moderation/reports/{id}/resolve` body `ResolveReportRequest`
  → 204 / 400 (invalid action) / 404.
- Author moderation reuses existing `POST /api/moderation/users/{id}/actions`.

## Frontend

- `types/api.ts`: `ReportCategory`, `CommentReport` (matches dto),
  `CreateReportRequest`.
- `services/reportsApi.ts`: `create(commentId, body)`, `listPending()`,
  `resolve(id, akcija)`.
- `components/forum/ReportCommentModal.tsx`: category buttons + optional textarea;
  calls `reportsApi.create`; success toast/close.
- Comment row (in `ForumDiscussion`): a small **Prijavi** (flag) action, shown for
  authenticated users on non-own, non-deleted comments → opens the modal.
- `pages/ModerationPanel.tsx` at `/moderacija` (ProtectedRoute
  `['moderator','administrator']`): pending reports list — comment text, author
  (link to forum/profile), reporter, category badge + opis, date; per-row buttons
  **Obriši komentar** / **Odbaci** / **Moderiši autora** (opens `ModerationModal`).
  Empty state when none.
- `Layout` nav: a `Moderacija` item (Shield icon) rendered only when
  `hasRole('moderator','administrator')`, in both mobile + desktop nav.
- `App.tsx`: protected route for `/moderacija`.

## Out of Scope (v1)
User search, admin/mod listing, notifications (future panel sections); reporting
posts/news; report history view; email alerts.

## Testing (TDD)
- **Service:** create happy path; own-comment rejected; duplicate-pending
  rejected; missing/deleted comment rejected; resolve-delete soft-deletes comment
  + resolves all its pending reports; resolve-dismiss marks odbaceno; pending list
  enriches + excludes deleted-comment reports.
- **Endpoints:** report-create returns 201/400/404; moderation reports GET list;
  resolve 204/404 (controller-level with fakes).
- **Frontend (vitest):** ReportCommentModal submits category+opis; ModerationPanel
  renders reports and triggers resolve; nav shows Moderacija only for mod/admin.

## Acceptance Criteria
1. A registered user reports a comment (category + optional note); cannot report
   own or report twice while pending.
2. Moderators see pending reports in `/moderacija` and can delete the comment,
   dismiss the report, or moderate the author.
3. Deleting a comment resolves all its pending reports; the panel/nav is invisible
   to guests and registered users.
4. New backend + frontend tests pass; existing suites stay green.
