using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IMatchSyncService
{
    Task<MatchSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken);

    Task<MatchSyncResponse> SyncAllSeasonsAsync(CancellationToken cancellationToken);
}

public sealed class MatchSyncService : IMatchSyncService
{
    private const int PremierLeagueTournamentId = 17;

    private readonly IFootballProvider _footballProvider;
    private readonly IRepository<Team> _teamsRepository;
    private readonly IRepository<Match> _matchesRepository;
    private readonly IProviderRequestPacer _requestPacer;

    public MatchSyncService(
        IFootballProvider footballProvider,
        IRepository<Team> teamsRepository,
        IRepository<Match> matchesRepository,
        IProviderRequestPacer requestPacer)
    {
        _footballProvider = footballProvider;
        _teamsRepository = teamsRepository;
        _matchesRepository = matchesRepository;
        _requestPacer = requestPacer;
    }

    public async Task<MatchSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken)
    {
        var seasons = await GetSeasonsAsync(cancellationToken);
        var season = seasons.FirstOrDefault(item => item.Id == seasonId)
            ?? throw new MatchSyncException($"Season {seasonId} was not found.");

        var teamCache = await BuildTeamCacheAsync(cancellationToken);
        var matchCache = await BuildMatchCacheAsync(cancellationToken);

        return await SyncSeasonCoreAsync(season, teamCache, matchCache, cancellationToken);
    }

    public async Task<MatchSyncResponse> SyncAllSeasonsAsync(CancellationToken cancellationToken)
    {
        var seasons = await GetSeasonsAsync(cancellationToken);
        var teamCache = await BuildTeamCacheAsync(cancellationToken);
        var matchCache = await BuildMatchCacheAsync(cancellationToken);

        var total = 0;
        var created = 0;
        var updated = 0;
        var teamsCreated = 0;

        foreach (var season in seasons)
        {
            var result = await SyncSeasonCoreAsync(season, teamCache, matchCache, cancellationToken);
            total += result.Total;
            created += result.Created;
            updated += result.Updated;
            teamsCreated += result.TeamsCreated;
        }

        return new MatchSyncResponse(total, created, updated, teamsCreated);
    }

    private async Task<MatchSyncResponse> SyncSeasonCoreAsync(
        FootballSeason season,
        Dictionary<int, Team> teamCache,
        Dictionary<int, Match> matchCache,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<FootballEvent> events;

        try
        {
            await _requestPacer.WaitAsync(cancellationToken);
            events = await _footballProvider.GetSeasonEventsAsync(
                PremierLeagueTournamentId,
                season.Id,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new MatchSyncException("FootAPI events could not be loaded.", exception);
        }

        var seasonLabel = NormalizeSeasonLabel(season.Year);
        var created = 0;
        var updated = 0;
        var teamsCreated = 0;

        foreach (var footballEvent in events)
        {
            var (homeId, homeCreated) = await EnsureTeamAsync(
                footballEvent.HomeTeamId, footballEvent.HomeTeamName, footballEvent.HomeTeamCode, teamCache, cancellationToken);
            var (awayId, awayCreated) = await EnsureTeamAsync(
                footballEvent.AwayTeamId, footballEvent.AwayTeamName, footballEvent.AwayTeamCode, teamCache, cancellationToken);

            if (homeCreated)
            {
                teamsCreated++;
            }

            if (awayCreated)
            {
                teamsCreated++;
            }

            if (matchCache.TryGetValue(footballEvent.EventId, out var existing))
            {
                ApplyEvent(existing, footballEvent, homeId, awayId, seasonLabel);
                await _matchesRepository.UpdateAsync(existing.Id, existing, cancellationToken);
                updated++;
            }
            else
            {
                var match = new Match { ProviderId = footballEvent.EventId };
                ApplyEvent(match, footballEvent, homeId, awayId, seasonLabel);
                var createdMatch = await _matchesRepository.CreateAsync(match, cancellationToken);
                matchCache[footballEvent.EventId] = createdMatch;
                created++;
            }
        }

        return new MatchSyncResponse(events.Count, created, updated, teamsCreated);
    }

    private async Task<(string TeamId, bool Created)> EnsureTeamAsync(
        int providerId,
        string name,
        string code,
        Dictionary<int, Team> teamCache,
        CancellationToken cancellationToken)
    {
        if (teamCache.TryGetValue(providerId, out var existing))
        {
            return (existing.Id, false);
        }

        var team = new Team
        {
            ProviderId = providerId,
            Naziv = name.Trim(),
            Skracenica = code.Trim(),
            Stadion = string.Empty,
            Osnovan = 0,
            LogoUrl = string.Empty,
            Bodovi = 0,
            Pozicija = 0
        };

        var createdTeam = await _teamsRepository.CreateAsync(team, cancellationToken);
        teamCache[providerId] = createdTeam;
        return (createdTeam.Id, true);
    }

    private static void ApplyEvent(Match match, FootballEvent footballEvent, string homeId, string awayId, string seasonLabel)
    {
        match.ProviderId = footballEvent.EventId;
        match.DomacinId = homeId;
        match.GostId = awayId;
        match.Kolo = footballEvent.Round;
        match.Sezona = seasonLabel;
        match.Datum = DateTimeOffset.FromUnixTimeSeconds(footballEvent.StartTimestamp).UtcDateTime;
        match.GolDomacin = footballEvent.HomeScore;
        match.GolGost = footballEvent.AwayScore;
        match.Status = MapStatus(footballEvent.StatusType);
    }

    private async Task<Dictionary<int, Team>> BuildTeamCacheAsync(CancellationToken cancellationToken)
    {
        var teams = await _teamsRepository.GetAllAsync(cancellationToken);
        var cache = new Dictionary<int, Team>();

        foreach (var team in teams.Where(item => item.ProviderId is not null))
        {
            cache.TryAdd(team.ProviderId!.Value, team);
        }

        return cache;
    }

    private async Task<Dictionary<int, Match>> BuildMatchCacheAsync(CancellationToken cancellationToken)
    {
        var matches = await _matchesRepository.GetAllAsync(cancellationToken);
        var cache = new Dictionary<int, Match>();

        foreach (var match in matches.Where(item => item.ProviderId is not null))
        {
            cache.TryAdd(match.ProviderId!.Value, match);
        }

        return cache;
    }

    private async Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _footballProvider.GetSeasonsAsync(PremierLeagueTournamentId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new MatchSyncException("FootAPI seasons could not be loaded.", exception);
        }
    }

    private static string MapStatus(string statusType)
    {
        return statusType.ToLowerInvariant() switch
        {
            "finished" => "zavrsena",
            "inprogress" => "uzivo",
            _ => "zakazana"
        };
    }

    public static string NormalizeSeasonLabel(string label)
    {
        var parts = label.Split('/');

        if (parts.Length != 2)
        {
            return label;
        }

        var start = parts[0].Trim();
        var end = parts[1].Trim();

        if (start.Length == 4 || start.Length != 2 || !int.TryParse(start, out var startYear))
        {
            return label;
        }

        var century = startYear >= 90 ? "19" : "20";
        return $"{century}{start}/{end}";
    }
}
