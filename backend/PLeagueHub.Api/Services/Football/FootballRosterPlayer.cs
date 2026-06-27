namespace PLeagueHub.Api.Services.Football;

public sealed record FootballRosterPlayer(
    int ProviderId,
    string Name,
    string Position,
    int Number,
    string Country);
