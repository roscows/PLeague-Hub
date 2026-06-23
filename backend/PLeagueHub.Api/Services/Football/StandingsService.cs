using Microsoft.Extensions.Caching.Memory;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
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
    private readonly IRepository<Team> _teamsRepository;
    private readonly IMemoryCache _cache;

    public StandingsService(
        IFootballProvider footballProvider,
        IRepository<Team> teamsRepository,
        IMemoryCache cache)
    {
        _footballProvider = footballProvider;
        _teamsRepository = teamsRepository;
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

        var teams = await _teamsRepository.GetAllAsync(cancellationToken);
        var logoByProviderId = teams
            .Where(team => team.ProviderId is not null)
            .GroupBy(team => team.ProviderId!.Value)
            .ToDictionary(group => group.Key, group => group.First().LogoUrl);

        var rows = standings
            .OrderByDescending(standing => standing.Points)
            .ThenByDescending(standing => standing.GoalDifference)
            .ThenByDescending(standing => standing.GoalsFor)
            .Select((standing, index) => new StandingRowResponse(
                index + 1,
                standing.ProviderId,
                standing.Name,
                standing.Abbreviation,
                logoByProviderId.GetValueOrDefault(standing.ProviderId, string.Empty),
                standing.Played,
                standing.Wins,
                standing.Draws,
                standing.Losses,
                standing.GoalsFor,
                standing.GoalsAgainst,
                standing.GoalDifference,
                standing.Points))
            .ToArray();

        _cache.Set(cacheKey, rows, seasonId == CurrentSeasonId ? CurrentSeasonTtl : PastSeasonTtl);
        return rows;
    }
}
