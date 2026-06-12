namespace PLeagueHub.Api.Requests;

public sealed record CreateCommentRequest
{
    public string Tekst { get; init; } = string.Empty;
}
