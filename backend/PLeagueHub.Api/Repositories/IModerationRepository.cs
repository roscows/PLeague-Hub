using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Repositories;

public interface IModerationRepository
{
    Task<User?> GetUserAsync(string id, CancellationToken cancellationToken = default);

    Task<Post?> GetPostAsync(string id, CancellationToken cancellationToken = default);

    Task<Comment?> GetCommentAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> SetActiveModerationAsync(
        string userId,
        ActiveModeration? state,
        CancellationToken cancellationToken = default);

    Task<bool> ExpireModerationAsync(
        string userId,
        DateTime expectedStart,
        ModerationAction action,
        CancellationToken cancellationToken = default);

    Task CreateActionAsync(ModerationAction action, CancellationToken cancellationToken = default);

    Task<bool> SetPostDeletedAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> SetCommentDeletedAsync(string id, CancellationToken cancellationToken = default);

    Task<bool> SetPostPinnedAsync(
        string id,
        bool pinned,
        string moderatorId,
        DateTime now,
        CancellationToken cancellationToken = default);

    Task<bool> SetCommentPinnedAsync(
        string id,
        bool pinned,
        string moderatorId,
        DateTime now,
        CancellationToken cancellationToken = default);
}
