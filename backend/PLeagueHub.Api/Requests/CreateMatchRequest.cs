namespace PLeagueHub.Api.Requests;

public sealed class CreateMatchRequest
{
    public string DomacinId { get; set; } = string.Empty;

    public string GostId { get; set; } = string.Empty;

    public DateTime Datum { get; set; }

    public int Kolo { get; set; }

    public string Sezona { get; set; } = string.Empty;

    public int? GolDomacin { get; set; }

    public int? GolGost { get; set; }

    public string Status { get; set; } = string.Empty;
}
