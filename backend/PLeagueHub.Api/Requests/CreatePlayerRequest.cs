namespace PLeagueHub.Api.Requests;

public sealed class CreatePlayerRequest
{
    public string TeamId { get; set; } = string.Empty;

    public string Ime { get; set; } = string.Empty;

    public string Prezime { get; set; } = string.Empty;

    public string Pozicija { get; set; } = string.Empty;

    public string Nacionalnost { get; set; } = string.Empty;

    public int Golovi { get; set; }

    public int Asistencije { get; set; }

    public double Ocena { get; set; }
}
