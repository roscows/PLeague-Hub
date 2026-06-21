using PLeagueHub.Api.Models;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public enum ModerationError
{
    None,
    Unauthorized,
    Validation,
    Forbidden,
    NotFound
}

public sealed record ModerationResult<T>(T? Value, ModerationError Error, string? Message)
{
    public static ModerationResult<T> Success(T? value) => new(value, ModerationError.None, null);
    public static ModerationResult<T> Failure(ModerationError error, string message) => new(default, error, message);
}

public sealed record ModerationAccessResult(
    bool Allowed,
    ModerationStateResponse? State,
    string? Message);

public interface IModerationService
{
    Task<ModerationResult<ModerationStateResponse>> ApplyAsync(
        string targetId,
        string? actorId,
        CreateModerationActionRequest request,
        CancellationToken cancellationToken = default);

    Task<ModerationResult<ModerationStateResponse>> RevokeAsync(
        string targetId,
        string? actorId,
        CancellationToken cancellationToken = default);

    Task<ModerationAccessResult> CheckLoginAsync(User user, CancellationToken cancellationToken = default);

    Task<ModerationAccessResult> CheckLoginByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<ModerationAccessResult> CheckForumWriteAsync(string userId, CancellationToken cancellationToken = default);

    Task<ModerationStateResponse?> GetActiveStateAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> CanModerateContentAsync(string actorId, string authorId, CancellationToken cancellationToken = default);
}
