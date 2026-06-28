using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public sealed class SearchService
{
    private readonly IRepository<Team> _teamsRepository;
    private readonly IRepository<PlayerSeasonStatDocument> _playerStatsRepository;

    public SearchService(
        IRepository<Team> teamsRepository,
        IRepository<PlayerSeasonStatDocument> playerStatsRepository)
    {
        _teamsRepository = teamsRepository;
        _playerStatsRepository = playerStatsRepository;
    }

    public async Task<IReadOnlyCollection<SearchResultResponse>> SearchAsync(
        string? query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedQuery) || normalizedQuery.Length < 2)
        {
            return [];
        }

        var resultLimit = Math.Clamp(limit, 1, 20);
        var teamsTask = _teamsRepository.GetAllAsync(cancellationToken);
        var statsTask = _playerStatsRepository.GetAllAsync(cancellationToken);
        await Task.WhenAll(teamsTask, statsTask);

        var teams = await teamsTask;
        var stats = await statsTask;

        var teamResults = teams
            .Where(team => team.ProviderId is > 0 && Matches(team.Naziv, normalizedQuery))
            .Select(team => new SearchResultResponse
            {
                Id = team.Id ?? string.Empty,
                ProviderId = team.ProviderId!.Value,
                Type = "team",
                Name = team.Naziv,
                Subtitle = team.Skracenica,
                ImageUrl = team.LogoUrl
            });

        // Player season stats hold the same player once per season; collapse to the
        // most recent season so each player appears once with their latest club.
        var playerResults = stats
            .Where(stat => stat.ProviderId > 0 && Matches(stat.Ime, normalizedQuery))
            .GroupBy(stat => stat.ProviderId)
            .Select(group => group.OrderByDescending(stat => stat.Sezona, StringComparer.Ordinal).First())
            .Select(stat => new SearchResultResponse
            {
                Id = stat.Id ?? string.Empty,
                ProviderId = stat.ProviderId,
                Type = "player",
                Name = stat.Ime,
                Subtitle = stat.TeamNaziv,
                ImageUrl = stat.TeamLogoUrl
            });

        return teamResults
            .Concat(playerResults)
            .OrderBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .Take(resultLimit)
            .ToList();
    }

    private static bool Matches(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
