namespace PLeagueHub.Api.Services.Football;

public sealed record FootballPlayerProfile(
    int ProviderId,
    string Name,
    string Position,
    int Height,
    DateTime? DateOfBirth,
    string Country,
    int TeamId,
    string TeamName);
