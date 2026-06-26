using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IMatchDetailService
{
    Task<MatchDetailResponse?> GetAsync(string matchId, CancellationToken cancellationToken);
}

// Match detail (statistics, incidents, lineups) is fetched from FootApi on the
// first view and persisted to MongoDB, so later views (and demos) read from the
// database without any live FootApi call.
public sealed class MatchDetailService : IMatchDetailService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<Match> _matches;
    private readonly IRepository<Team> _teams;
    private readonly IRepository<MatchDetailDocument> _details;
    private readonly IFootballProvider _provider;
    private readonly IProviderRequestPacer _pacer;

    public MatchDetailService(
        IRepository<Match> matches,
        IRepository<Team> teams,
        IRepository<MatchDetailDocument> details,
        IFootballProvider provider,
        IProviderRequestPacer pacer)
    {
        _matches = matches;
        _teams = teams;
        _details = details;
        _provider = provider;
        _pacer = pacer;
    }

    public async Task<MatchDetailResponse?> GetAsync(string matchId, CancellationToken cancellationToken)
    {
        var match = await _matches.GetByIdAsync(matchId, cancellationToken);

        if (match is null)
        {
            return null;
        }

        var header = await BuildHeaderAsync(match, cancellationToken);

        if (match.ProviderId is not int providerId)
        {
            return new MatchDetailResponse(header, [], [], null);
        }

        var detail = await GetOrFetchDetailAsync(matchId, providerId, cancellationToken);
        return new MatchDetailResponse(header, detail.Statistics, detail.Incidents, detail.Lineups);
    }

    private async Task<CachedDetail> GetOrFetchDetailAsync(string matchId, int providerId, CancellationToken cancellationToken)
    {
        var stored = await _details.FindOneAsync(document => document.MatchId == matchId, cancellationToken);

        if (stored is not null)
        {
            var cached = JsonSerializer.Deserialize<CachedDetail>(stored.DetailJson, JsonOptions);

            if (cached is not null)
            {
                return cached;
            }
        }

        IReadOnlyCollection<FootballStatItem> stats;
        IReadOnlyCollection<FootballIncident> incidents;
        FootballLineups? lineups;

        try
        {
            await _pacer.WaitAsync(cancellationToken);
            stats = await _provider.GetMatchStatisticsAsync(providerId, cancellationToken);
            await _pacer.WaitAsync(cancellationToken);
            incidents = await _provider.GetMatchIncidentsAsync(providerId, cancellationToken);
            await _pacer.WaitAsync(cancellationToken);
            lineups = await _provider.GetMatchLineupsAsync(providerId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new MatchDetailUnavailableException("FootAPI match detail could not be loaded.", exception);
        }

        var detail = new CachedDetail(MapStats(stats), MapIncidents(incidents), MapLineups(lineups));

        await _details.CreateAsync(
            new MatchDetailDocument
            {
                MatchId = matchId,
                ProviderId = providerId,
                DetailJson = JsonSerializer.Serialize(detail, JsonOptions),
                FetchedAt = DateTime.UtcNow
            },
            cancellationToken);

        return detail;
    }

    private async Task<MatchHeaderDto> BuildHeaderAsync(Match match, CancellationToken cancellationToken)
    {
        var home = await _teams.GetByIdAsync(match.DomacinId, cancellationToken);
        var away = await _teams.GetByIdAsync(match.GostId, cancellationToken);

        return new MatchHeaderDto(
            ToTeamDto(home),
            ToTeamDto(away),
            match.GolDomacin,
            match.GolGost,
            match.Kolo,
            match.Sezona,
            match.Status,
            match.Datum);
    }

    private static MatchTeamDto ToTeamDto(Team? team)
        => new(team?.Naziv ?? "Nepoznat tim", team?.Skracenica ?? string.Empty, team?.LogoUrl ?? string.Empty);

    private static IReadOnlyCollection<StatItemDto> MapStats(IReadOnlyCollection<FootballStatItem> stats)
        => stats.Select(item => new StatItemDto(item.Name, item.Home, item.Away)).ToArray();

    private static IReadOnlyCollection<IncidentDto> MapIncidents(IReadOnlyCollection<FootballIncident> incidents)
        => incidents
            .OrderBy(incident => incident.Minute)
            .Select(incident => new IncidentDto(
                incident.Type,
                incident.Minute,
                incident.IsHome,
                BuildIncidentText(incident)))
            .ToArray();

    private static string BuildIncidentText(FootballIncident incident)
        => incident.Type switch
        {
            "substitution" => $"{incident.PlayerInName} ↑ / {incident.PlayerOutName} ↓",
            _ => string.IsNullOrWhiteSpace(incident.PlayerName) ? incident.Detail : incident.PlayerName
        };

    private static LineupsDto? MapLineups(FootballLineups? lineups)
    {
        if (lineups?.Home is null || lineups.Away is null)
        {
            return null;
        }

        return new LineupsDto(lineups.Confirmed, MapLineupTeam(lineups.Home), MapLineupTeam(lineups.Away));
    }

    private static LineupTeamDto MapLineupTeam(FootballLineupTeam team)
        => new(
            team.Formation,
            team.Players
                .Select(player => new LineupPlayerDto(player.Name, player.Number, player.IsSubstitute, player.Position))
                .ToArray());

    private sealed record CachedDetail(
        IReadOnlyCollection<StatItemDto> Statistics,
        IReadOnlyCollection<IncidentDto> Incidents,
        LineupsDto? Lineups);
}
