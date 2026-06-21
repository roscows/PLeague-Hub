namespace PLeagueHub.Api.Requests;

public sealed record UpdateMatchRequest
{
    public DateTime Datum { get; init; }

    public int Kolo { get; init; }

    public string Sezona { get; init; } = string.Empty;

    public int? GolDomacin { get; init; }

    public int? GolGost { get; init; }

    public string Status { get; init; } = string.Empty;
}
