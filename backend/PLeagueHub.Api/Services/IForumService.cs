using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public enum ForumError
{
    None,
    Unauthorized,
    Validation,
    NotFound,
    InvalidParent,
    SelfVote
}

public sealed record ForumResult<T>(T? Value, ForumError Error, string? Message)
    where T : class
{
    public static ForumResult<T> Success(T value) => new(value, ForumError.None, null);

    public static ForumResult<T> Failure(ForumError error, string message) => new(null, error, message);
}

public interface IForumService
{
    Task<PagedResponse<ForumTopicResponse>> GetTopicsAsync(
        ForumListRequest request,
        CancellationToken cancellationToken = default);

    Task<ForumDiscussionResponse?> GetDiscussionAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForumCommentResponse>?> GetCommentsAsync(
        string postId,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ForumResult<ForumDiscussionResponse>> CreateDiscussionAsync(
        CreateForumPostRequest request,
        string? authorId,
        CancellationToken cancellationToken = default);

    Task<ForumResult<ForumCommentResponse>> CreateCommentAsync(
        string postId,
        CreateCommentRequest request,
        string? authorId,
        CancellationToken cancellationToken = default);

    Task<ForumResult<ForumVoteResponse>> VoteAsync(
        string commentId,
        string? userId,
        int value,
        CancellationToken cancellationToken = default);

    Task<ForumResult<ForumVoteResponse>> RemoveVoteAsync(
        string commentId,
        string? userId,
        CancellationToken cancellationToken = default);
}
