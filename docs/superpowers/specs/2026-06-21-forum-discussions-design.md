# Forum Discussions Design

**Date:** 2026-06-21
**Status:** Approved for planning

## Goal

Redesign the PLeague Hub forum around the dense, readable structure of the HLTV Counter-Strike forum while preserving the portal's existing light, Flashscore-inspired visual language. All user-facing text is Serbian Latin. The phase covers the topic list, discussion detail view, three-level replies, and authenticated like/dislike voting.

## Experience

### Topic list

- Route: `/forum`.
- Breadcrumb: `Forum > Premier liga`.
- Header contains `Premier League diskusije` and `Nova tema`.
- Dense table columns: `Tema`, `Odgovori`, `Autor`, and `Aktivnost`.
- The entire topic row links to `/forum/{id}`.
- Highlighted topics such as forum rules use a distinct row treatment.
- Relative Serbian activity labels are used, for example `pre nekoliko sekundi`, `pre 5 minuta`, and `juce`.
- Search and server-side pagination return 20 topics per page by default.
- Guests can read discussions. Creating a topic requires authentication.
- On narrow screens, author metadata moves below the title while reply count and recent activity remain visible.

### Discussion detail

- Route: `/forum/:id`.
- Breadcrumb: `Forum > Premier liga > {naslov}`.
- The original post is shown first with title, author, content, and creation time.
- Comments are numbered in chronological display order and connected visually to their replies.
- Replies support up to three visible nesting levels. Replies beyond the third level remain associated with their target but render at level three.
- Each comment exposes `Odgovori`, `Svidja mi se`, and `Ne svidja mi se` actions.
- An inline reply editor opens directly below the selected comment.
- Deleted comments with descendants remain as `Komentar je uklonjen` so the conversation tree is not broken.
- Mobile layouts reduce indentation while retaining reply relationships.

## Voting Rules

- Only authenticated users can vote.
- A user has at most one vote per comment.
- Vote values are `1` for like and `-1` for dislike.
- A user can change or withdraw a vote.
- Users cannot vote on their own comments.
- Guests can see like/dislike totals; attempting to vote redirects them to login.
- All rules are enforced by the API, independently of frontend state.

## Data Model

### Comment changes

Add nullable `ParentCommentId` to `Comment`. Existing comments remain root-level comments because the field is absent or null.

### CommentVotes collection

Add a `CommentVote` model and MongoDB collection with:

- `Id`
- `CommentId`
- `UserId`
- `Value` (`1` or `-1`)
- `CreatedAt`
- `UpdatedAt`

Create a unique compound index on `(CommentId, UserId)`. Add supporting indexes for comment vote aggregation and comment lookup by post and parent.

Votes are kept in a separate collection instead of embedded arrays to provide an enforceable uniqueness constraint and avoid unbounded comment documents.

## API Design

Existing write routes remain compatible. Public reads return purpose-built DTOs instead of exposing persistence models directly.

### Topic list

`GET /api/forum?search={text}&page={number}&pageSize={size}`

Returns paginated topic summaries containing topic identity, title, author display data, reply count, creation time, and latest activity data.

### Discussion

`GET /api/forum/{id}`

Returns the original post with author display data and discussion metadata.

`GET /api/forum/{id}/comments`

Returns comments with parent identity, author display data, chronological number, like/dislike totals, and the authenticated caller's vote when available. The frontend builds the bounded tree from the flat response.

### Replies and votes

`POST /api/forum/{id}/comments` accepts comment text and optional `parentCommentId`. The API verifies that the parent belongs to the same discussion.

`PUT /api/forum/comments/{commentId}/vote` accepts `value: 1 | -1` and creates or changes the caller's vote.

`DELETE /api/forum/comments/{commentId}/vote` withdraws the caller's vote.

Expected failures use the existing API error format: unauthenticated access, missing resources, invalid parents, self-voting, invalid vote values, and duplicate/concurrent write conflicts.

## Frontend Structure

- Refactor `Forum.tsx` into the topic-list page.
- Add a `ForumDiscussion.tsx` route for `/forum/:id`.
- Add focused components for topic rows, original posts, comment threads, comment cards, voting controls, and inline reply forms.
- Extend `forumApi.ts` and API types for paginated summaries, enriched comments, replies, and votes.
- Preserve the existing light background, restrained panels, red brand accent, compact typography, and square-to-small-radius controls.
- Use semantic table markup on desktop and an accessible responsive row layout on mobile.

## State And Error Handling

- Topic search is debounced and resets pagination.
- Topic list and discussion detail have loading, empty, not-found, and recoverable error states.
- Vote controls update optimistically and roll back if the API rejects the operation.
- Comment submission prevents duplicate sends, clears only after success, and refreshes or inserts the returned comment.
- Expired authentication follows the existing Axios/AuthContext handling.

## Verification

- Backend tests cover the unique vote rule, vote changes and withdrawal, self-vote rejection, authentication, parent validation, three-level reply data, deleted-parent preservation, search, and pagination.
- Frontend tests cover topic navigation, Serbian labels, responsive metadata, tree construction, nesting cap, guest behavior, inline replies, and optimistic voting rollback.
- Swagger exposes all new request and response schemas and permits manual verification with JWT authentication.
- Desktop and mobile browser checks verify density, wrapping, indentation, and absence of clipping or overlap.

## Out Of Scope

- Real-time updates and WebSockets.
- Notifications for replies.
- User reputation derived from votes.
- Arbitrary-depth visual nesting.
- Dedicated moderator controls for pinning topics; highlighted system topics can be added in a later moderation phase.
