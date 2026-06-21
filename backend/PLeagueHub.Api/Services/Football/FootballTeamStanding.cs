namespace PLeagueHub.Api.Services.Football;

public sealed record FootballTeamStanding(
    int ProviderId,
    string Name,
    string Abbreviation,
    int Position,
    int Points);
