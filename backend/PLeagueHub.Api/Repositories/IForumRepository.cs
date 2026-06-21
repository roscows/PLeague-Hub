using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Repositories;

public sealed record ForumQuery(string? Search, int Page, int PageSize)
{
    public int Skip => (Page - 1) * PageSize;

    public static ForumQuery Create(string? search, int page, int pageSize)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        return new ForumQuery(
            normalizedSearch,
            Math.Max(1, page),
            pageSize <= 0 ? 20 : Math.Clamp(pageSize, 1, 50));
    }
}

public sealed record ForumTopicPage(IReadOnlyList<Post> Items, long Total);

public interface IForumRepository
{
    Task<ForumTopicPage> GetTopicsAsync(ForumQuery query, CancellationToken cancellationToken = default);

    Task<Post?> GetVisibleDiscussionAsync(string id, CancellationToken cancellationToken = default);

    Task<Comment?> GetCommentAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> GetCommentsAsync(string postId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> GetCommentsForPostsAsync(
        IReadOnlyCollection<string> postIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, User>> GetUsersAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommentVote>> GetVotesAsync(
        IReadOnlyCollection<string> commentIds,
        CancellationToken cancellationToken = default);

    Task<Post> CreateDiscussionAsync(Post post, CancellationToken cancellationToken = default);

    Task<Comment> CreateCommentAsync(Comment comment, CancellationToken cancellationToken = default);

    Task<CommentVote?> GetVoteAsync(
        string commentId,
        string userId,
        CancellationToken cancellationToken = default);

    Task UpsertVoteAsync(CommentVote vote, CancellationToken cancellationToken = default);

    Task<bool> DeleteVoteAsync(
        string commentId,
        string userId,
        CancellationToken cancellationToken = default);
}
