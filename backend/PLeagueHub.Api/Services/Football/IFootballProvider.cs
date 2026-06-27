using System.Text.Json;

namespace PLeagueHub.Api.Services.Football;

public interface IFootballProvider
{
    Task<JsonDocument> SearchAsync(
        string term,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default);

    Task<FootballTeamLogo> GetTeamLogoAsync(
        int providerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(
        int tournamentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FootballEvent>> GetSeasonEventsAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FootballStatItem>> GetMatchStatisticsAsync(
        int eventId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FootballIncident>> GetMatchIncidentsAsync(
        int eventId,
        CancellationToken cancellationToken = default);

    Task<FootballLineups?> GetMatchLineupsAsync(
        int eventId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FootballPlayerStat>> GetBestPlayersAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default);
}
