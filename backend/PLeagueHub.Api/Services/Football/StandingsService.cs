using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IStandingsService
{
    Task<IReadOnlyCollection<SeasonResponse>> GetSeasonsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StandingRowResponse>> GetStandingsAsync(
        string season,
        CancellationToken cancellationToken);
}

// Standings are computed from the matches stored in MongoDB (no live FootApi
// dependency), so the table is always available and consistent with Results.
public sealed class StandingsService : IStandingsService
{
    private const string FinishedStatus = "zavrsena";

    private readonly IRepository<Match> _matches;
    private readonly IRepository<Team> _teams;

    public StandingsService(IRepository<Match> matches, IRepository<Team> teams)
    {
        _matches = matches;
        _teams = teams;
    }

    public async Task<IReadOnlyCollection<SeasonResponse>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        var matches = await _matches.GetAllAsync(cancellationToken);

        return matches
            .Select(match => match.Sezona)
            .Where(season => !string.IsNullOrWhiteSpace(season))
            .Distinct()
            .OrderByDescending(season => season, StringComparer.Ordinal)
            .Select(season => new SeasonResponse(season))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<StandingRowResponse>> GetStandingsAsync(
        string season,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(season))
        {
            return [];
        }

        var matches = (await _matches.GetAllAsync(cancellationToken))
            .Where(match =>
                string.Equals(match.Sezona, season, StringComparison.Ordinal)
                && string.Equals(match.Status, FinishedStatus, StringComparison.Ordinal)
                && match.GolDomacin.HasValue
                && match.GolGost.HasValue)
            .ToList();

        var teams = (await _teams.GetAllAsync(cancellationToken))
            .GroupBy(team => team.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var table = new Dictionary<string, Tally>(StringComparer.Ordinal);

        foreach (var match in matches)
        {
            var home = GetTally(table, match.DomacinId);
            var away = GetTally(table, match.GostId);
            var homeGoals = match.GolDomacin!.Value;
            var awayGoals = match.GolGost!.Value;

            home.Apply(homeGoals, awayGoals);
            away.Apply(awayGoals, homeGoals);
        }

        return table
            .OrderByDescending(entry => entry.Value.Points)
            .ThenByDescending(entry => entry.Value.GoalDifference)
            .ThenByDescending(entry => entry.Value.GoalsFor)
            .ThenBy(entry => TeamName(teams, entry.Key), StringComparer.Ordinal)
            .Select((entry, index) => BuildRow(index + 1, teams, entry.Key, entry.Value))
            .ToArray();
    }

    private static Tally GetTally(Dictionary<string, Tally> table, string teamId)
    {
        if (!table.TryGetValue(teamId, out var tally))
        {
            tally = new Tally();
            table[teamId] = tally;
        }

        return tally;
    }

    private static string TeamName(IReadOnlyDictionary<string, Team> teams, string teamId)
        => teams.TryGetValue(teamId, out var team) ? team.Naziv : string.Empty;

    private static StandingRowResponse BuildRow(
        int position,
        IReadOnlyDictionary<string, Team> teams,
        string teamId,
        Tally tally)
    {
        teams.TryGetValue(teamId, out var team);

        return new StandingRowResponse(
            position,
            team?.ProviderId ?? 0,
            team?.Naziv ?? "Nepoznat tim",
            team?.Skracenica ?? string.Empty,
            team?.LogoUrl ?? string.Empty,
            tally.Played,
            tally.Wins,
            tally.Draws,
            tally.Losses,
            tally.GoalsFor,
            tally.GoalsAgainst,
            tally.GoalDifference,
            tally.Points);
    }

    private sealed class Tally
    {
        public int Played { get; private set; }
        public int Wins { get; private set; }
        public int Draws { get; private set; }
        public int Losses { get; private set; }
        public int GoalsFor { get; private set; }
        public int GoalsAgainst { get; private set; }

        public int GoalDifference => GoalsFor - GoalsAgainst;
        public int Points => (Wins * 3) + Draws;

        public void Apply(int scored, int conceded)
        {
            Played++;
            GoalsFor += scored;
            GoalsAgainst += conceded;

            if (scored > conceded)
            {
                Wins++;
            }
            else if (scored < conceded)
            {
                Losses++;
            }
            else
            {
                Draws++;
            }
        }
    }
}
