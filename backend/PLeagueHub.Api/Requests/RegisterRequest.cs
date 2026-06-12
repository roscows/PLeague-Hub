namespace PLeagueHub.Api.Requests;

public sealed record RegisterRequest
{
    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
