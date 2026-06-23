using Microsoft.Extensions.Caching.Memory;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IStandingsService
{
    Task<IReadOnlyCollection<SeasonResponse>> GetSeasonsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StandingRowResponse>> GetStandingsAsync(
        int seasonId,
        CancellationToken cancellationToken);
}

public sealed class StandingsService : IStandingsService
{
    public const int PremierLeagueTournamentId = 17;
    public const int CurrentSeasonId = 96668;

    private static readonly TimeSpan CurrentSeasonTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PastSeasonTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan SeasonsTtl = TimeSpan.FromHours(24);

    private readonly IFootballProvider _footballProvider;
    private readonly ITeamLogoCache _logoCache;
    private readonly IProviderRequestPacer _requestPacer;
    private readonly IMemoryCache _cache;

    public StandingsService(
        IFootballProvider footballProvider,
        ITeamLogoCache logoCache,
        IProviderRequestPacer requestPacer,
        IMemoryCache cache)
    {
        _footballProvider = footballProvider;
        _logoCache = logoCache;
        _requestPacer = requestPacer;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<SeasonResponse>> GetSeasonsAsync(
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue("standings:seasons", out IReadOnlyCollection<SeasonResponse>? cached)
            && cached is not null)
        {
            return cached;
        }

        IReadOnlyCollection<SeasonResponse> seasons;

        try
        {
            var providerSeasons = await _footballProvider.GetSeasonsAsync(
                PremierLeagueTournamentId,
                cancellationToken);

            seasons = providerSeasons
                .Select(season => new SeasonResponse(
                    season.Id,
                    string.IsNullOrWhiteSpace(season.Year) ? season.Name : season.Year))
                .ToArray();
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException
                or System.Text.Json.JsonException)
        {
            seasons = [new SeasonResponse(CurrentSeasonId, "Trenutna sezona")];
        }

        if (seasons.Count == 0)
        {
            seasons = [new SeasonResponse(CurrentSeasonId, "Trenutna sezona")];
        }

        _cache.Set("standings:seasons", seasons, SeasonsTtl);
        return seasons;
    }

    public async Task<IReadOnlyCollection<StandingRowResponse>> GetStandingsAsync(
        int seasonId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"standings:{seasonId}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyCollection<StandingRowResponse>? cached)
            && cached is not null)
        {
            return cached;
        }

        IReadOnlyCollection<FootballTeamStanding> standings;

        try
        {
            standings = await _footballProvider.GetTeamStandingsAsync(
                PremierLeagueTournamentId,
                seasonId,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException
                or System.Text.Json.JsonException)
        {
            throw new StandingsUnavailableException(
                "FootAPI standings could not be loaded.",
                exception);
        }

        var ordered = standings
            .OrderByDescending(standing => standing.Points)
            .ThenByDescending(standing => standing.GoalDifference)
            .ThenByDescending(standing => standing.GoalsFor)
            .ToArray();

        var rows = new List<StandingRowResponse>(ordered.Length);

        for (var index = 0; index < ordered.Length; index++)
        {
            var standing = ordered[index];
            var logoUrl = await ResolveLogoUrlAsync(standing.ProviderId, cancellationToken);

            rows.Add(new StandingRowResponse(
                index + 1,
                standing.ProviderId,
                standing.Name,
                standing.Abbreviation,
                logoUrl,
                standing.Played,
                standing.Wins,
                standing.Draws,
                standing.Losses,
                standing.GoalsFor,
                standing.GoalsAgainst,
                standing.GoalDifference,
                standing.Points));
        }

        IReadOnlyCollection<StandingRowResponse> result = rows;
        _cache.Set(cacheKey, result, seasonId == CurrentSeasonId ? CurrentSeasonTtl : PastSeasonTtl);
        return result;
    }

    private async Task<string> ResolveLogoUrlAsync(int providerId, CancellationToken cancellationToken)
    {
        if (providerId <= 0)
        {
            return string.Empty;
        }

        if (_logoCache.Exists(providerId))
        {
            return _logoCache.GetPublicUrl(providerId);
        }

        try
        {
            await _requestPacer.WaitAsync(cancellationToken);
            var logo = await _footballProvider.GetTeamLogoAsync(providerId, cancellationToken);
            await _logoCache.SaveAsync(providerId, logo, cancellationToken);
            return _logoCache.GetPublicUrl(providerId);
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or InvalidDataException
                or InvalidOperationException
                or IOException
                or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }
}
