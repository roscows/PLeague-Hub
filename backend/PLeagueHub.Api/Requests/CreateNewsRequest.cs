namespace PLeagueHub.Api.Requests;

public sealed record CreateNewsRequest
{
    public string Naslov { get; init; } = string.Empty;

    public string Sadrzaj { get; init; } = string.Empty;

    public string Kategorija { get; init; } = "premier_league";

    public string Pouzdanost { get; init; } = "pouzdan_izvor";

    public string? ImageUrl { get; init; }

    public string? OriginalUrl { get; init; }
}
