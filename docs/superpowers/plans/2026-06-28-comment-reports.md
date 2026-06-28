# Comment Reports + Moderator Panel — Implementation Plan

**Reference spec:** `docs/superpowers/specs/2026-06-28-comment-reports-design.md`

**Goal:** Registered users report forum comments; mod/admin act on them in a
protected `/moderacija` panel (delete comment / dismiss / moderate author).

**Build/test:** backend `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter <Name> -p:UseAppHost=false` (kill running `PLeagueHub.Api.dll` first; on first run after a rebuild the SAC catastrophic-load may appear once — rerun). Frontend `cd frontend && npx vitest run <path>`. Commit production only (tests are git-ignored).

---

## Task R1: CommentReport collection + repo wiring

**Create:** `Models/CommentReportDocument.cs` (fields per spec; `Status` default `na_cekanju`).
**Modify:** `Configuration/MongoDbSettings.cs` (`CommentReportsCollectionName = "CommentReports"`), `Data/MongoContext.cs` (collection + property + `GetCollection` case), `Data/MongoIndexInitializer.cs` (`idx_commentReports_status_datum`, `idx_commentReports_komentar_prijavio`).

- [ ] Build → 0 errors. Commit: `Add CommentReport collection`.

---

## Task R2: CommentReportService + DTOs

**Create:** `Responses/CommentReportDto.cs`, `Requests/CreateCommentReportRequest.cs`, `Requests/ResolveReportRequest.cs`, `Services/CommentReportService.cs` (+ `ICommentReportService`, result enums `ReportCreateResult`, `ReportResolveResult`).
**Modify:** `Program.cs` (register `ICommentReportService`).
**Test:** `CommentReportServiceTests.cs`.

- [ ] Ctor `(IRepository<CommentReportDocument> reports, IModerationRepository moderation, IRepository<User> users, TimeProvider time)`.
- [ ] `CreateAsync` validation order per spec (InvalidCategory → CommentNotFound(missing/Obrisan) → CannotReportOwn → DuplicatePending(any na_cekanju by reporter for comment via reports.GetAllAsync filter) → create Created). Allowed categories const `["spam","uvrede","offtopic","ostalo"]`.
- [ ] `GetPendingAsync`: reports where Status==na_cekanju; for each resolve comment via `moderation.GetCommentAsync` (skip if null/Obrisan), author+reporter via `users.GetAllAsync` dict; order DatumPrijave desc; map to dto.
- [ ] `ResolveAsync`: load report (NotFound); `obrisi` → `moderation.SetCommentDeletedAsync(KomentarId)` + set all na_cekanju reports of that KomentarId to reseno/komentar_obrisan/ResioId/ResenoAt (update each via reports.UpdateAsync); `odbaci` → this report odbaceno; else InvalidAction.
- [ ] Tests (TDD): create happy; own→CannotReportOwn; duplicate→DuplicatePending; deleted/missing comment→CommentNotFound; bad category→InvalidCategory; resolve obrisi soft-deletes + resolves siblings; resolve odbaci; pending excludes deleted-comment reports + enriches. Fakes: `FakeRepository<CommentReportDocument>` (GetAll/Create/Update/GetById), `FakeModerationRepository` (GetCommentAsync/SetCommentDeletedAsync record), `FakeRepository<User>`.
- [ ] Run → PASS. Commit: `Add comment report service`.

---

## Task R3: Report-create endpoint (ForumController)

**Modify:** `Controllers/ForumController.cs` (inject `ICommentReportService`; add `POST comments/{commentId}/report` `[Authorize]`, reporter = `User.FindFirstValue(ClaimTypes.NameIdentifier)`, map result enum → 201/200/400/404).
**Test:** `CommentReportEndpointTests.cs` (controller-level with fake service: Created→201, DuplicatePending→200, CannotReportOwn/InvalidCategory→400, CommentNotFound→404).

- [ ] Confirm ForumController constructor params; append the service last.
- [ ] Run → PASS; build. Commit: `Expose comment report endpoint`.

---

## Task R4: Moderation panel endpoints (ModerationController)

**Modify:** `Controllers/ModerationController.cs` (inject `ICommentReportService`; `GET reports` → 200 list; `POST reports/{id}/resolve` body `ResolveReportRequest` → 204/400/404).
**Test:** add to `CommentReportEndpointTests.cs` (GET returns list; resolve Resolved→204, NotFound→404, InvalidAction→400).

- [ ] Run → PASS; build. Commit: `Expose moderation report queue endpoints`.

---

## Task R5: Frontend types + reportsApi

**Modify:** `types/api.ts` (`ReportCategory = 'spam'|'uvrede'|'offtopic'|'ostalo'`; `CommentReport` matching dto; `CreateReportRequest`).
**Create:** `services/reportsApi.ts` (`create(commentId, body)` POST `/api/forum/comments/{commentId}/report`; `listPending()` GET `/api/moderation/reports`; `resolve(id, akcija)` POST `/api/moderation/reports/{id}/resolve`).
**Test:** `services/reportsApi.test.ts`.

- [ ] Run → PASS. Commit: `Add report types and api client`.

---

## Task R6: Report action on comments

**Create:** `components/forum/ReportCommentModal.tsx` (category buttons + optional textarea; `reportsApi.create`; error via `getApiErrorMessage`; `onClose`/`onReported`).
**Modify:** `pages/ForumDiscussion.tsx` (render a **Prijavi** flag button on each non-own, non-deleted comment for authenticated users → opens modal with that comment id).
**Test:** `components/forum/ReportCommentModal.test.tsx` (submits selected category + opis).

- [ ] Inspect `ForumDiscussion.tsx` comment rendering + how it knows current user / author id before wiring the button.
- [ ] Run → PASS; build. Commit: `Add comment report action and modal`.

---

## Task R7: Moderacija panel page + route + nav

**Create:** `pages/ModerationPanel.tsx` (loads `reportsApi.listPending()`; list rows with comment text, author link, reporter, category badge + opis, date; buttons Obriši komentar / Odbaci / Moderiši autora → `ModerationModal`; empty state), `pages/ModerationPanel.test.tsx`.
**Modify:** `App.tsx` (ProtectedRoute `['moderator','administrator']` → `moderacija`), `components/Layout.tsx` (render a `Moderacija` nav item, Shield icon, only when `hasRole('moderator','administrator')` — both mobile + desktop lists).

- [ ] Tests: panel renders a pending report + dismiss calls resolve; (nav role-gating covered by panel route guard). Run → PASS.
- [ ] `npx vitest run` full + `npm run build`. Commit: `Add moderator panel with comment reports`.

---

## Final Verification
- [ ] Backend: kill api, full `dotnet test … -p:UseAppHost=false` green; build 0 errors.
- [ ] Frontend: `npx vitest run` green; `npm run build` succeeds.
- [ ] Live (api on 5000): register/login a normal user, report a comment; login admin, open `/moderacija`, delete comment + dismiss another; confirm nav hidden for guest. Push branch.
