using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public sealed record AuthServiceResult
{
    public int StatusCode { get; init; }

    public string? Error { get; init; }

    public AuthResponse? Response { get; init; }

    public ModerationStateResponse? Moderation { get; init; }

    public static AuthServiceResult Ok(AuthResponse response)
    {
        return new AuthServiceResult { StatusCode = StatusCodes.Status200OK, Response = response };
    }

    public static AuthServiceResult Created(AuthResponse response)
    {
        return new AuthServiceResult { StatusCode = StatusCodes.Status201Created, Response = response };
    }

    public static AuthServiceResult BadRequest(string error)
    {
        return new AuthServiceResult { StatusCode = StatusCodes.Status400BadRequest, Error = error };
    }

    public static AuthServiceResult Conflict(string error)
    {
        return new AuthServiceResult { StatusCode = StatusCodes.Status409Conflict, Error = error };
    }

    public static AuthServiceResult Unauthorized(string error, ModerationStateResponse? moderation = null)
    {
        return new AuthServiceResult
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            Error = error,
            Moderation = moderation
        };
    }
}
