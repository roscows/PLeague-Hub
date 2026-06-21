# Forum Moderation and Accurate Timing Design

## Scope

This phase extends the PLeague Hub forum with unlimited compact reply nesting,
role-aware moderation, temporary account restrictions, topic and comment
pinning, accurate relative timestamps, and test-file Git exclusions. It also
removes the forum breadcrumb and topic composer from the visible frontend.

## Forum Presentation

- Remove the `Forum > Premier liga` breadcrumb from the forum topic list.
- Remove the `Nova tema` button and composer from the forum page. The existing
  API may remain available for Swagger and future use.
- Number comments in normal depth-first display order. A reply displayed after
  `#1` becomes `#2`, regardless of when it was created.
- Remove the three-level depth limit.
- Indent each reply by 8 px up to six visual levels. Deeper replies remain at
  the sixth-level width and show `odgovor na #X` in the header.
- Use thin vertical guide lines to preserve the parent-child relationship.
- Pinned comments appear once, directly below the original post, together with
  their descendant subtree. They are removed from their normal tree position.
- Pinned comments retain the number calculated from the unpinned tree so links
  and references remain stable while viewing the page.

## Pinning

- `Post.Istaknut` remains the source of truth for pinned forum topics.
- `Comment` gains pin state, pin timestamp, and the moderator who pinned it.
- Pinned topics sort above unpinned topics on the main forum.
- Pinned comments sort above the normal comment tree within a discussion.
- Moderators may pin content authored by registered users only.
- Administrators may pin content authored by registered users and moderators.
- No administrator can moderate another administrator through this interface.
- Pin and unpin operations are idempotent.

## Account Moderation

Clicking an eligible comment author's username opens a moderation modal with:

- current username, role, and account state;
- measure selection: `Mute` or `Suspenzija`;
- durations: one hour, 24 hours, seven days, 30 days, or permanent;
- a required reason visible to the affected user;
- the current active measure and an option to revoke it;
- a warning when role hierarchy blocks the action;
- a clear confirmation action.

Mute allows login and public reading but blocks topic creation, commenting, and
voting. Suspension blocks login and authenticated requests. Expired measures
become inactive automatically. A muted user sees the reason and remaining time;
a suspended user receives the same information when login is rejected.

Role hierarchy is enforced by the backend:

- moderators may act only on registered users and their content;
- administrators may act on registered users and moderators and their content;
- administrators cannot act on other administrators;
- UI visibility is convenience only and is not a security boundary.

## Persistence

`User` stores the active moderation state as an embedded object containing the
measure type, reason, start time, optional expiry, and acting moderator ID.

A new `ModerationActions` MongoDB collection stores the immutable audit history:
target user, acting moderator, action type, measure type, reason, start time,
expiry, and event timestamp. Applying, revoking, and first observing an expired
measure produce audit records. Expiry processing must be idempotent.

The existing `Aktivan` account flag remains compatible with account state but
the moderation service becomes the single place that decides whether login or
a forum mutation is allowed.

## Content Moderation

- Comment actions use a compact right-aligned overflow menu.
- Eligible actions are pin/unpin and soft delete.
- A deleted comment with descendants remains as `Komentar je uklonjen`.
- A deleted comment without descendants is omitted from the response.
- Existing post deletion remains soft deletion and follows the same role
  hierarchy based on the author's role.

## API Surface

The moderation API adds:

```text
POST   /api/moderation/users/{id}/actions
DELETE /api/moderation/users/{id}/action
DELETE /api/moderation/comments/{id}
PUT    /api/moderation/posts/{id}/pin
DELETE /api/moderation/posts/{id}/pin
PUT    /api/moderation/comments/{id}/pin
DELETE /api/moderation/comments/{id}/pin
```

The create-action request contains measure type, duration selection, and reason.
Responses expose enough moderation state for the approved modal and user-facing
restriction message. Swagger documents validation, authentication, hierarchy,
and missing-resource responses.

## Accurate Time Display

- Forum relative times always derive from stored UTC timestamps.
- A shared relative-time component refreshes once per minute while visible.
- Seed forum timestamps derive from seed execution time so demo labels remain
  truthful after a fresh database setup.
- `Match` gains optional `ZavrsenaAt`.
- External synchronization stores `ZavrsenaAt` only when the provider supplies
  a real completion timestamp.
- Finished matches display `FT - pre X sati` when `ZavrsenaAt` exists, otherwise
  only `FT`. The system never estimates completion by adding match duration.

## Git Test Exclusions

Tests remain available locally and are still used during development, but are
removed from the Git index and ignored in future commits:

```text
backend/PLeagueHub.Api.Tests/
frontend/src/**/*.test.ts
frontend/src/**/*.test.tsx
frontend/src/test/
```

The index removal must use cached-only deletion so local files are preserved.
The visual companion workspace `.superpowers/` is also ignored.

## Error Handling and Verification

- Backend authorization returns Serbian error messages for active measures and
  forbidden role hierarchy actions.
- Invalid duration, missing reason, unsupported measure, missing content, and
  repeated moderation operations have explicit outcomes.
- Local tests cover hierarchy, expiry, mute/suspension enforcement, numbering,
  unlimited nesting, pin ordering, and timing calculations.
- Final verification includes backend tests, frontend tests, production build,
  Swagger route inspection, MongoDB-backed smoke checks, and desktop/mobile
  visual inspection.
