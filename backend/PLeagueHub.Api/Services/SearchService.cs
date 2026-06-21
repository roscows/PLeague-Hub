using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public sealed class SearchService
{
    private readonly IRepository<Team> _teamsRepository;
    private readonly IRepository<Player> _playersRepository;

    public SearchService(
        IRepository<Team> teamsRepository,
        IRepository<Player> playersRepository)
    {
        _teamsRepository = teamsRepository;
        _playersRepository = playersRepository;
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
        var playersTask = _playersRepository.GetAllAsync(cancellationToken);
        await Task.WhenAll(teamsTask, playersTask);

        var teams = await teamsTask;
        var players = await playersTask;
        var teamMap = teams
            .Where(team => !string.IsNullOrWhiteSpace(team.Id))
            .ToDictionary(team => team.Id!, StringComparer.Ordinal);

        var teamResults = teams
            .Where(team => StartsWith(team.Naziv, normalizedQuery))
            .Select(team => new SearchResultResponse
            {
                Id = team.Id ?? string.Empty,
                Type = "team",
                Name = team.Naziv,
                Subtitle = team.Skracenica,
                ImageUrl = team.LogoUrl
            });

        var playerResults = players
            .Where(player =>
                StartsWith(player.Ime, normalizedQuery)
                || StartsWith(player.Prezime, normalizedQuery)
                || StartsWith($"{player.Ime} {player.Prezime}", normalizedQuery))
            .Select(player =>
            {
                teamMap.TryGetValue(player.TeamId, out var team);

                return new SearchResultResponse
                {
                    Id = player.Id ?? string.Empty,
                    Type = "player",
                    Name = $"{player.Ime} {player.Prezime}".Trim(),
                    Subtitle = team?.Naziv ?? player.Pozicija,
                    ImageUrl = team?.LogoUrl ?? string.Empty
                };
            });

        return teamResults
            .Concat(playerResults)
            .OrderBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
            .Take(resultLimit)
            .ToList();
    }

    private static bool StartsWith(string value, string query) =>
        value.StartsWith(query, StringComparison.OrdinalIgnoreCase);
}
