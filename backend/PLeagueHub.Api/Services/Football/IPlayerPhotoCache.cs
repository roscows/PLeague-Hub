namespace PLeagueHub.Api.Services.Football;

public interface IPlayerPhotoCache
{
    bool Exists(int playerId);

    Task SaveAsync(
        int playerId,
        FootballTeamLogo photo,
        CancellationToken cancellationToken = default);

    string GetPublicUrl(int playerId);
}
