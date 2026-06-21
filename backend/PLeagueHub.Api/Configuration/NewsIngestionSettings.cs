namespace PLeagueHub.Api.Configuration;

public sealed class NewsIngestionSettings
{
    public bool WorkerEnabled { get; init; }

    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(5);

    public int MaxResponseBytes { get; init; } = 2_000_000;

    public int MaxRedirects { get; init; } = 3;

    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(15);
}
