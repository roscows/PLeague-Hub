using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IPlayerStatsSyncService
{
    Task<PlayerStatsSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken);
}

public sealed class PlayerStatsSyncService : IPlayerStatsSyncService
{
    private const int PremierLeagueTournamentId = 17;

    private readonly IFootballProvider _provider;
    private readonly IRepository<Team> _teams;
    private readonly IRepository<PlayerSeasonStatDocument> _stats;
    private readonly IProviderRequestPacer _pacer;

    public PlayerStatsSyncService(
        IFootballProvider provider,
        IRepository<Team> teams,
        IRepository<PlayerSeasonStatDocument> stats,
        IProviderRequestPacer pacer)
    {
        _provider = provider;
        _teams = teams;
        _stats = stats;
        _pacer = pacer;
    }

    public async Task<PlayerStatsSyncResponse> SyncSeasonAsync(int seasonId, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<FootballSeason> seasons;
        IReadOnlyCollection<FootballPlayerStat> players;

        try
        {
            seasons = await _provider.GetSeasonsAsync(PremierLeagueTournamentId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new PlayerStatsSyncException("FootAPI seasons could not be loaded.", exception);
        }

        var season = seasons.FirstOrDefault(item => item.Id == seasonId)
            ?? throw new PlayerStatsSyncException($"Season {seasonId} was not found.");
        var label = MatchSyncService.NormalizeSeasonLabel(season.Year);

        try
        {
            await _pacer.WaitAsync(cancellationToken);
            players = await _provider.GetBestPlayersAsync(PremierLeagueTournamentId, seasonId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new PlayerStatsSyncException("FootAPI best players could not be loaded.", exception);
        }

        var teamByProvider = (await _teams.GetAllAsync(cancellationToken))
            .Where(team => team.ProviderId is not null)
            .GroupBy(team => team.ProviderId!.Value)
            .ToDictionary(group => group.Key, group => group.First());

        var existing = (await _stats.GetAllAsync(cancellationToken))
            .Where(stat => string.Equals(stat.Sezona, label, StringComparison.Ordinal))
            .GroupBy(stat => stat.ProviderId)
            .ToDictionary(group => group.Key, group => group.First());

        var created = 0;
        var updated = 0;

        foreach (var player in players)
        {
            teamByProvider.TryGetValue(player.TeamId, out var team);
            var naziv = team?.Naziv ?? player.TeamName;
            var logo = team?.LogoUrl ?? string.Empty;

            if (existing.TryGetValue(player.ProviderId, out var current))
            {
                current.Ime = player.Name;
                current.TeamNaziv = naziv;
                current.TeamLogoUrl = logo;
                current.Golovi = player.Goals;
                current.Asistencije = player.Assists;
                current.Odigrano = player.Appearances;
                await _stats.UpdateAsync(current.Id, current, cancellationToken);
                updated++;
            }
            else
            {
                var document = new PlayerSeasonStatDocument
                {
                    Sezona = label,
                    ProviderId = player.ProviderId,
                    Ime = player.Name,
                    TeamNaziv = naziv,
                    TeamLogoUrl = logo,
                    Golovi = player.Goals,
                    Asistencije = player.Assists,
                    Odigrano = player.Appearances
                };
                var createdDoc = await _stats.CreateAsync(document, cancellationToken);
                existing[player.ProviderId] = createdDoc;
                created++;
            }
        }

        return new PlayerStatsSyncResponse(players.Count, created, updated);
    }
}
