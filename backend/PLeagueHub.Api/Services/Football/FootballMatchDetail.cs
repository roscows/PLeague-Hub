namespace PLeagueHub.Api.Services.Football;

public sealed record FootballStatItem(string Name, string Home, string Away);

public sealed record FootballIncident(
    string Type,
    int Minute,
    int AddedTime,
    bool IsHome,
    string PlayerName,
    string PlayerInName,
    string PlayerOutName,
    string Detail,
    string Class);

public sealed record FootballLineupPlayer(string Name, int Number, bool IsSubstitute, string Position);

public sealed record FootballLineupTeam(string Formation, IReadOnlyCollection<FootballLineupPlayer> Players);

public sealed record FootballLineups(bool Confirmed, FootballLineupTeam? Home, FootballLineupTeam? Away);
