namespace PLeagueHub.Api.Services.Football;

public sealed record FootballTeamDetails(
    int ProviderId,
    string Name,
    string Stadium,
    int Founded,
    string Manager,
    string Country);
