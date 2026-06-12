namespace PLeagueHub.Api.Requests;

public sealed record CreateNewsRequest
{
    public string Naslov { get; init; } = string.Empty;

    public string Sadrzaj { get; init; } = string.Empty;
}
