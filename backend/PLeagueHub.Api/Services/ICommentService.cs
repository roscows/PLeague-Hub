using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public interface ICommentService
{
    Task<IReadOnlyList<ForumCommentResponse>?> GetCommentsAsync(
        string postId,
        string? currentUserId,
        CancellationToken cancellationToken = default);

    Task<ForumResult<ForumCommentResponse>> CreateAsync(
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
