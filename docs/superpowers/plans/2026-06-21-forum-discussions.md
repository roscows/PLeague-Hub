# Forum Discussions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a dense Serbian Premier League forum with searchable topic summaries, dedicated discussion pages, three-level replies, and authenticated like/dislike voting.

**Architecture:** A specialized Mongo forum repository performs bounded queries and aggregations. `ForumService` owns validation, DTO composition, and voting rules; React uses separate list/detail routes and a flat-to-tree helper capped at three visual levels.

**Tech Stack:** .NET 10, ASP.NET Core, MongoDB.Driver, JWT, Swagger, xUnit, React 19, TypeScript, Axios, React Router, Tailwind CSS, Vitest, Testing Library.

---

## File Map

**Backend**

- Create `Models/CommentVote.cs`; modify `Models/Comment.cs` and `Models/Post.cs`.
- Modify Mongo settings, `MongoContext.cs`, and `MongoIndexInitializer.cs`.
- Create `Repositories/IForumRepository.cs` and `MongoForumRepository.cs`.
- Create `Services/IForumService.cs` and `ForumService.cs`.
- Add forum request/response DTOs; replace raw-model responses in `ForumController.cs`.
- Extend `DatabaseSeeder.cs` with stable forum comments/votes.
- Add focused infrastructure, service, endpoint, and seeder tests.

**Frontend**

- Extend `types/api.ts` and `services/forumApi.ts`.
- Add `utils/forumTree.ts` and `utils/relativeTime.ts`.
- Add `components/forum/ForumTopicTable.tsx`, `ForumComposer.tsx`, `ForumComment.tsx`, and `ForumThread.tsx`.
- Rewrite `pages/Forum.tsx`; create `pages/ForumDiscussion.tsx`; extend `App.tsx`.
- Add Testing Library setup and focused page/service/helper tests.

## Task 1: Persist Replies And Votes

**Files:** `Models/Comment.cs`, `Models/CommentVote.cs`, `Models/Post.cs`, `Configuration/MongoDbSettings.cs`, `appsettings.json`, `Data/MongoContext.cs`, `Data/MongoIndexInitializer.cs`, `ForumInfrastructureTests.cs`

- [ ] Write failing reflection tests for nullable `Comment.ParentCommentId`, `Post.Istaknut`, the `CommentVote` fields, and the default `CommentVotes` collection name.
- [ ] Run `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter ForumInfrastructureTests`; expect missing-type failures.
- [ ] Add `CommentVote : BaseDocument` with ObjectId-backed `CommentId`/`UserId`, `Value`, `CreatedAt`, and `UpdatedAt`.
- [ ] Add nullable ObjectId-backed `ParentCommentId` to `Comment`.
- [ ] Add BSON-backed `Istaknut` to `Post`, defaulting to false for existing documents.
- [ ] Wire `CommentVotes` through settings, appsettings, context, and `GetCollection<TDocument>()`.
- [ ] Add indexes `idx_comments_postId_parentId_datumKreiranja`, unique `uq_commentVotes_commentId_userId`, and `idx_commentVotes_commentId_value`.
- [ ] Re-run the focused test and full backend suite; expect PASS.
- [ ] Commit only these files with `git commit -m "Add forum reply and vote persistence"`.

Core model:

```csharp
public sealed class CommentVote : BaseDocument
{
    [BsonElement("commentId"), BsonRepresentation(BsonType.ObjectId)]
    public string CommentId { get; set; } = string.Empty;

    [BsonElement("userId"), BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("value")]
    public int Value { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

## Task 2: Add Bounded Mongo Forum Queries

**Files:** `Repositories/IForumRepository.cs`, `Repositories/MongoForumRepository.cs`, `Program.cs`, `ForumRepositoryTests.cs`

- [ ] Write failing tests for title search, page bounds, latest-activity ordering, reply totals, batch user lookup, and vote totals.
- [ ] Run `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter ForumRepositoryTests`; expect missing repository failures.
- [ ] Define repository methods for paged visible posts, one visible discussion, post comments, users by IDs, votes by comment IDs, writes, vote upsert, and vote deletion.
- [ ] Implement Mongo filters for visible discussions, escaped case-insensitive title regex, highlighted-first/activity-second ordering, `Skip`, `Limit`, and server-side count.
- [ ] Batch-load related comments/users/votes for only the page or discussion. Never call `GetAllAsync()` in forum reads.
- [ ] Register `IForumRepository` while preserving all current team-sync registrations in `Program.cs`.
- [ ] Run focused and full backend tests; expect PASS.
- [ ] Commit with `git commit -m "Add bounded forum repository queries"`.

Required contract:

```csharp
Task<(IReadOnlyList<Post> Items, long Total)> GetTopicsAsync(
    string? search, int page, int pageSize, CancellationToken cancellationToken);
Task<IReadOnlyList<Comment>> GetCommentsAsync(string postId, CancellationToken cancellationToken);
Task<IReadOnlyList<CommentVote>> GetVotesAsync(
    IEnumerable<string> commentIds, CancellationToken cancellationToken);
Task UpsertVoteAsync(CommentVote vote, CancellationToken cancellationToken);
Task<bool> DeleteVoteAsync(string commentId, string userId, CancellationToken cancellationToken);
```

## Task 3: Build Forum Service And DTOs

**Files:** forum request/response DTOs, `Services/IForumService.cs`, `Services/ForumService.cs`, `ForumServiceTests.cs`

- [ ] Write failing tests for paging normalization, enriched topic metadata, deleted-parent preservation, parent-topic validation, comment numbering, vote creation/change/withdrawal, invalid values, unauthenticated access, and self-vote rejection.
- [ ] Run `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter ForumServiceTests`; expect missing service failures.
- [ ] Add `ForumListRequest` defaults `Page=1`, `PageSize=20`; cap size at 50.
- [ ] Add optional `ParentCommentId` to `CreateCommentRequest` and create `VoteCommentRequest(int Value)`.
- [ ] Add `PagedResponse<T>`, `ForumTopicResponse` including `Istaknut`, `ForumDiscussionResponse`, and `ForumCommentResponse`.
- [ ] Map deleted comments with descendants to `Komentar je uklonjen`; omit deleted leaves.
- [ ] Number comments chronologically, aggregate likes/dislikes, and return `CurrentUserVote`.
- [ ] Reject a parent from another discussion, any value outside `1/-1`, and votes on the caller's own comment.
- [ ] Run focused/full backend tests; expect PASS.
- [ ] Commit with `git commit -m "Add forum discussion service"`.

Comment response:

```csharp
public sealed record ForumCommentResponse(
    string Id,
    string PostId,
    string? ParentCommentId,
    string AuthorId,
    string AuthorUsername,
    string AuthorRole,
    string Text,
    DateTime CreatedAt,
    bool IsDeleted,
    int Number,
    int Likes,
    int Dislikes,
    int? CurrentUserVote);
```

## Task 4: Expose REST And Swagger Contracts

**Files:** `Controllers/ForumController.cs`, `Program.cs`, `ForumWriteEndpointsTests.cs`, `ContentEndpointsTests.cs`

- [ ] Rewrite endpoint tests for DTO reads, search/paging binding, guest reads, authenticated replies, vote create/change/delete, guest 401, invalid/self vote 400, and missing resource 404.
- [ ] Run the endpoint tests; expect failures against raw persistence-model responses.
- [ ] Inject `IForumService` into the controller and expose:
  `GET/POST /api/forum`, `GET /api/forum/{id}`, `GET/POST /api/forum/{id}/comments`, `PUT/DELETE /api/forum/comments/{commentId}/vote`.
- [ ] Add concrete `ProducesResponseType` declarations so Swagger displays every request/response schema and JWT requirement.
- [ ] Register `IForumService` in `Program.cs`.
- [ ] Run `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj` and `dotnet build backend/PLeagueHub.Api/PLeagueHub.Api.csproj`; expect PASS/0 errors.
- [ ] Commit with `git commit -m "Expose threaded forum API"`.

## Task 5: Seed A Representative Conversation

**Files:** `Data/Seeding/DatabaseSeeder.cs`, `DatabaseSeederTests.cs`

- [ ] Write failing tests that require several discussions, root/nested comments, valid parent relationships, valid non-self votes, and identical counts after two seed runs.
- [ ] Run `dotnet test backend/PLeagueHub.Api.Tests/PLeagueHub.Api.Tests.csproj --filter DatabaseSeederTests`; expect failure because comments/votes are absent.
- [ ] Inject comment/vote repositories and insert forum samples by stable ObjectId only when each ID is missing.
- [ ] Use Serbian Latin topic/comment content, including one explicitly highlighted rules topic. Do not reset or overwrite provider-synced sports data.
- [ ] Run seeder tests, then `dotnet run --project backend/PLeagueHub.Api -- --seed`; expect successful idempotent completion.
- [ ] Commit with `git commit -m "Seed Premier League forum discussions"`.

## Task 6: Add Frontend Contracts And Helpers

**Files:** `package.json`, lockfile, `vite.config.ts`, `src/test/setup.ts`, `types/api.ts`, `services/forumApi.ts`, `utils/forumTree.ts`, `utils/relativeTime.ts`, matching tests

- [ ] Run `npm install --save-dev @testing-library/react @testing-library/user-event @testing-library/jest-dom jsdom` in `frontend`.
- [ ] Write failing API tests for list query parameters and vote PUT/DELETE contracts.
- [ ] Write failing helper tests for chronological siblings, orphan roots, maximum visual depth 3, and Serbian second/minute/day labels.
- [ ] Run `npm test -- forumApi.test.ts forumTree.test.ts relativeTime.test.ts`; expect missing-contract failures.
- [ ] Add `PagedResponse<T>`, `ForumTopic`, `ForumDiscussion`, `ForumComment`, `ForumCommentNode`, and `CommentVoteValue = 1 | -1`.
- [ ] Implement list/detail/comments/create/reply/vote/withdraw methods in `forumApi`.
- [ ] Build the tree with a `Map`, preserve parent IDs, attach orphans at root, and cap rendered depth at 3.
- [ ] Implement deterministic `formatRelativeTime(value, now?)` returning Serbian Latin labels.
- [ ] Run focused tests and `npm run build`; expect PASS.
- [ ] Commit with `git commit -m "Add frontend forum data contracts"`.

## Task 7: Build The Topic List

**Files:** `components/forum/ForumTopicTable.tsx`, `ForumComposer.tsx`, `pages/Forum.tsx`, `pages/Forum.test.tsx`

- [ ] Write failing Testing Library tests for Serbian headings, desktop columns, debounced search/page reset, full-row links, guest login behavior, authenticated topic creation, loading, empty, error, and retry states.
- [ ] Run `npm test -- Forum.test.tsx`; expect failure against the old card list.
- [ ] Implement breadcrumb, `Premier League diskusije`, search, `Nova tema`, compact table, highlighted-row treatment, and pagination.
- [ ] Use the approved light theme: white surface, slate separators, red primary accent, restrained 8px-or-less radii, and dense typography.
- [ ] On mobile move author under the title while preserving reply count and activity; prevent title/control clipping.
- [ ] Debounce search by 300ms, reset page on search, prevent duplicate submits, and clear the composer only after success.
- [ ] Run the focused test and `npm run build`; expect PASS.
- [ ] Commit with `git commit -m "Build searchable forum topic list"`.

## Task 8: Build Discussion, Replies, And Voting

**Files:** `components/forum/ForumComment.tsx`, `ForumThread.tsx`, `pages/ForumDiscussion.tsx`, `ForumDiscussion.test.tsx`, `App.tsx`

- [ ] Write failing tests for breadcrumb/original post, numbered comments, three visual levels, inline replies, guest login redirect, disabled self-vote, selected vote, replacement/withdrawal, optimistic rollback, deleted placeholders, loading, and 404.
- [ ] Run `npm test -- ForumDiscussion.test.tsx`; expect missing-route/component failures.
- [ ] Render compact comment headers with number, username, role, time, and Lucide reply/like/dislike controls with visible counts/tooltips.
- [ ] Render bounded indentation and connector borders without nested decorative cards.
- [ ] Load post/comments, build the tree, insert successful replies, and prevent duplicate submissions.
- [ ] Implement one-vote optimistic transitions and restore the complete prior comment state on API error.
- [ ] Route guest actions to `/login`; disable voting where `comment.authorId === user.userId`.
- [ ] Add route `forum/:id` in `App.tsx`.
- [ ] Run all frontend tests and production build; expect PASS.
- [ ] Commit with `git commit -m "Build threaded forum discussion view"`.

## Task 9: Swagger And Visual Verification

**Files:** only targeted fixes discovered during verification

- [ ] Run the full backend tests, full frontend tests, and frontend production build; all must pass.
- [ ] Start/reuse MongoDB Docker, API at `http://localhost:5000`, and Vite at `http://localhost:3000`.
- [ ] In Swagger, authenticate and verify nested reply, like, dislike, vote replacement, withdrawal, self-vote rejection, search, and paging.
- [ ] At approximately 1440x900 and 390x844 inspect `/forum` and one discussion.
- [ ] Confirm density, Serbian labels, full-row navigation, connectors, inline reply placement, selected votes, wrapping, and no overlap/clipping.
- [ ] Run `git diff --check`; expect no whitespace errors.
- [ ] Run `git status --short`; ensure forum files remain separable from pre-existing team-logo work.
- [ ] Commit only verification fixes, if any, with `git commit -m "Polish forum responsive behavior"`.


