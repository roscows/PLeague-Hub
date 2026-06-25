using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IMatchDetailService
{
    Task<MatchDetailResponse?> GetAsync(string matchId, CancellationToken cancellationToken);
}

public sealed class MatchDetailService : IMatchDetailService
{
    private static readonly TimeSpan FinishedTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan LiveTtl = TimeSpan.FromMinutes(2);

    private readonly IRepository<Match> _matches;
    private readonly IRepository<Team> _teams;
    private readonly IFootballProvider _provider;
    private readonly IProviderRequestPacer _pacer;
    private readonly IMemoryCache _cache;

    public MatchDetailService(
        IRepository<Match> matches,
        IRepository<Team> teams,
        IFootballProvider provider,
        IProviderRequestPacer pacer,
        IMemoryCache cache)
    {
        _matches = matches;
        _teams = teams;
        _provider = provider;
        _pacer = pacer;
        _cache = cache;
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

        var detail = await GetCachedDetailAsync(providerId, match.Status, cancellationToken);
        return new MatchDetailResponse(header, detail.Statistics, detail.Incidents, detail.Lineups);
    }

    private async Task<CachedDetail> GetCachedDetailAsync(int providerId, string status, CancellationToken cancellationToken)
    {
        var cacheKey = $"match-detail:{providerId}";

        if (_cache.TryGetValue(cacheKey, out CachedDetail? cached) && cached is not null)
        {
            return cached;
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

        cached = new CachedDetail(MapStats(stats), MapIncidents(incidents), MapLineups(lineups));
        _cache.Set(cacheKey, cached, status == "zavrsena" ? FinishedTtl : LiveTtl);
        return cached;
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
