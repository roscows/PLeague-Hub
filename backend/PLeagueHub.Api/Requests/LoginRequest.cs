namespace PLeagueHub.Api.Requests;

public sealed record LoginRequest
{
    public string EmailOrUsername { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
