namespace PLeagueHub.Api.Responses;

public sealed record HealthResponse
{
    public string Status { get; init; } = "healthy";

    public string Service { get; init; } = "PLeague Hub API";

    public string Environment { get; init; } = string.Empty;

    public DateTime CheckedAtUtc { get; init; }
}
