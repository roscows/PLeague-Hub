namespace PLeagueHub.Api.Requests;

public sealed class CreateStatisticRequest
{
    public string MatchId { get; set; } = string.Empty;

    public string PlayerId { get; set; } = string.Empty;

    public int Golovi { get; set; }

    public int Asistencije { get; set; }

    public int Kartoni { get; set; }

    public int MinutiIgre { get; set; }

    public double Ocena { get; set; }
}
