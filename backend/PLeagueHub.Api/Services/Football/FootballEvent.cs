namespace PLeagueHub.Api.Services.Football;

public sealed record FootballEvent(
    int EventId,
    int Round,
    int HomeTeamId,
    string HomeTeamName,
    string HomeTeamCode,
    int AwayTeamId,
    string AwayTeamName,
    string AwayTeamCode,
    int? HomeScore,
    int? AwayScore,
    string StatusType,
    long StartTimestamp);
