namespace PLeagueHub.Api.Services.Football;

public sealed record FootballPlayerStat(
    int ProviderId,
    string Name,
    int TeamId,
    string TeamName,
    int Goals,
    int Assists,
    int Appearances);
