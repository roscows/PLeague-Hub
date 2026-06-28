namespace PLeagueHub.Api.Responses;

public sealed record SearchResultResponse
{
    public string Id { get; init; } = string.Empty;

    public int ProviderId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;
}
