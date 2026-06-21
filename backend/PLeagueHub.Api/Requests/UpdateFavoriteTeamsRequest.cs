namespace PLeagueHub.Api.Requests;

public sealed record UpdateFavoriteTeamsRequest
{
    public List<string> TeamIds { get; init; } = [];
}
