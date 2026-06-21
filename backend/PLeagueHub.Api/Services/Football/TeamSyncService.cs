using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public sealed class TeamSyncService
{
    private const int PremierLeagueTournamentId = 17;
    private readonly IFootballProvider _footballProvider;
    private readonly IRepository<Team> _teamsRepository;

    public TeamSyncService(
        IFootballProvider footballProvider,
        IRepository<Team> teamsRepository)
    {
        _footballProvider = footballProvider;
        _teamsRepository = teamsRepository;
    }

    public async Task<TeamSyncResponse> SyncAsync(
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<FootballTeamStanding> standings;

        try
        {
            standings = await _footballProvider.GetTeamStandingsAsync(
                PremierLeagueTournamentId,
                seasonId,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new TeamSyncException(
                "FootAPI standings could not be loaded.",
                exception);
        }

        ValidateStandings(standings);

        var existingTeams = await _teamsRepository.GetAllAsync(cancellationToken);
        var teamsByProviderId = new Dictionary<int, Team>();
        var teamsByName = new Dictionary<string, Team>(StringComparer.Ordinal);
        var unlinkedTeams = existingTeams
            .Where(team => team.ProviderId is null)
            .ToArray();
        var teamsByAbbreviation = unlinkedTeams
            .Where(team => !string.IsNullOrWhiteSpace(team.Skracenica))
            .GroupBy(team => NormalizeName(team.Skracenica))
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        foreach (var team in existingTeams)
        {
            if (team.ProviderId is int providerId)
            {
                teamsByProviderId.TryAdd(providerId, team);
            }
            else
            {
                teamsByName.TryAdd(NormalizeName(team.Naziv), team);
            }
        }

        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var standing in standings.OrderBy(item => item.Position))
        {
            Team? team = null;

            if (!teamsByProviderId.TryGetValue(standing.ProviderId, out team))
            {
                teamsByName.TryGetValue(NormalizeName(standing.Name), out team);
            }

            if (team is null && !string.IsNullOrWhiteSpace(standing.Abbreviation))
            {
                teamsByAbbreviation.TryGetValue(
                    NormalizeName(standing.Abbreviation),
                    out team);
            }

            if (team is null)
            {
                team = CreateTeam(standing);
                await _teamsRepository.CreateAsync(team, cancellationToken);
                teamsByProviderId.Add(standing.ProviderId, team);
                created++;
                continue;
            }

            if (IsUnchanged(team, standing))
            {
                skipped++;
                continue;
            }

            team.ProviderId = standing.ProviderId;
            team.Naziv = standing.Name.Trim();
            team.Skracenica = standing.Abbreviation.Trim();
            team.Bodovi = standing.Points;
            team.Pozicija = standing.Position;

            if (string.IsNullOrWhiteSpace(team.Id)
                || !await _teamsRepository.UpdateAsync(team.Id, team, cancellationToken))
            {
                throw new TeamSyncException($"Team '{standing.Name}' could not be updated.");
            }

            teamsByProviderId[standing.ProviderId] = team;
            updated++;
        }

        return new TeamSyncResponse(
            PremierLeagueTournamentId,
            seasonId,
            created,
            updated,
            skipped);
    }

    private static void ValidateStandings(IReadOnlyCollection<FootballTeamStanding> standings)
    {
        if (standings.Count == 0)
        {
            throw new TeamSyncException("FootAPI returned empty standings.");
        }

        if (standings.Any(item =>
                item.ProviderId <= 0
                || string.IsNullOrWhiteSpace(item.Name)
                || item.Position <= 0
                || item.Points < 0))
        {
            throw new TeamSyncException("FootAPI returned invalid standings data.");
        }

        if (standings.GroupBy(item => item.ProviderId).Any(group => group.Count() > 1))
        {
            throw new TeamSyncException("FootAPI returned duplicate team identifiers.");
        }
    }

    private static Team CreateTeam(FootballTeamStanding standing)
    {
        return new Team
        {
            ProviderId = standing.ProviderId,
            Naziv = standing.Name.Trim(),
            Skracenica = standing.Abbreviation.Trim(),
            Stadion = string.Empty,
            Osnovan = 0,
            LogoUrl = string.Empty,
            Bodovi = standing.Points,
            Pozicija = standing.Position
        };
    }

    private static bool IsUnchanged(Team team, FootballTeamStanding standing)
    {
        return team.ProviderId == standing.ProviderId
            && string.Equals(team.Naziv, standing.Name.Trim(), StringComparison.Ordinal)
            && string.Equals(team.Skracenica, standing.Abbreviation.Trim(), StringComparison.Ordinal)
            && team.Bodovi == standing.Points
            && team.Pozicija == standing.Position;
    }

    private static string NormalizeName(string name)
    {
        return string.Join(
                ' ',
                name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToUpperInvariant();
    }
}
