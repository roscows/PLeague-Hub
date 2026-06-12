namespace PLeagueHub.Api.Responses;

public sealed record AuthResponse
{
    public string UserId { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Uloga { get; init; } = string.Empty;

    public string Token { get; init; } = string.Empty;

    public DateTime ExpiresAt { get; init; }
}
