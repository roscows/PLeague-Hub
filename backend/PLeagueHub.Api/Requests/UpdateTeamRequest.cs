namespace PLeagueHub.Api.Requests;

public sealed class UpdateTeamRequest
{
    public string Naziv { get; set; } = string.Empty;

    public string Skracenica { get; set; } = string.Empty;

    public string Stadion { get; set; } = string.Empty;

    public int Osnovan { get; set; }

    public string LogoUrl { get; set; } = string.Empty;

    public int Bodovi { get; set; }

    public int Pozicija { get; set; }
}
