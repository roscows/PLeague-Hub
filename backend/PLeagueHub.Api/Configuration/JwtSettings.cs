namespace PLeagueHub.Api.Configuration;

public sealed class JwtSettings
{
    public string Secret { get; init; } = string.Empty;

    public string Issuer { get; init; } = "PLeagueHub";

    public string Audience { get; init; } = "PLeagueHub";

    public int ExpirationMinutes { get; init; } = 120;
}
