namespace PLeagueHub.Api.Services.Football;

public interface ITeamLogoCache
{
    bool Exists(int providerId);

    Task SaveAsync(
        int providerId,
        FootballTeamLogo logo,
        CancellationToken cancellationToken = default);

    string GetPublicUrl(int providerId);
}
