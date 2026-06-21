using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public sealed class TeamLogoSyncService
{
    private readonly IFootballProvider _footballProvider;
    private readonly IRepository<Team> _teamsRepository;
    private readonly ITeamLogoCache _logoCache;
    private readonly IProviderRequestPacer _requestPacer;

    public TeamLogoSyncService(
        IFootballProvider footballProvider,
        IRepository<Team> teamsRepository,
        ITeamLogoCache logoCache,
        IProviderRequestPacer requestPacer)
    {
        _footballProvider = footballProvider;
        _teamsRepository = teamsRepository;
        _logoCache = logoCache;
        _requestPacer = requestPacer;
    }

    public async Task<TeamLogoSyncResponse> SyncAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyCollection<Team> teams;

        try
        {
            teams = await _teamsRepository.GetAllAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new TeamLogoSyncException("Teams could not be loaded for logo synchronization.", exception);
        }

        var downloaded = 0;
        var updated = 0;
        var skipped = 0;
        var failedProviderIds = new List<int>();

        foreach (var team in teams
                     .Where(team => team.ProviderId is > 0)
                     .OrderBy(team => team.ProviderId))
        {
            var providerId = team.ProviderId!.Value;

            if (_logoCache.Exists(providerId))
            {
                var publicUrl = _logoCache.GetPublicUrl(providerId);
                skipped++;

                if (!string.Equals(team.LogoUrl, publicUrl, StringComparison.Ordinal))
                {
                    team.LogoUrl = publicUrl;

                    if (await UpdateTeamAsync(team, cancellationToken))
                    {
                        updated++;
                    }
                    else
                    {
                        failedProviderIds.Add(providerId);
                    }
                }

                continue;
            }

            try
            {
                await _requestPacer.WaitAsync(cancellationToken);
                var logo = await _footballProvider.GetTeamLogoAsync(providerId, cancellationToken);
                await _logoCache.SaveAsync(providerId, logo, cancellationToken);
                downloaded++;
                var publicUrl = _logoCache.GetPublicUrl(providerId);
                team.LogoUrl = publicUrl;

                if (await UpdateTeamAsync(team, cancellationToken))
                {
                    updated++;
                }
                else
                {
                    failedProviderIds.Add(providerId);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is HttpRequestException
                    or InvalidDataException
                    or InvalidOperationException
                    or IOException
                    or UnauthorizedAccessException)
            {
                failedProviderIds.Add(providerId);
            }
        }

        var distinctFailures = failedProviderIds.Distinct().Order().ToArray();
        return new TeamLogoSyncResponse(
            downloaded,
            updated,
            skipped,
            distinctFailures.Length,
            distinctFailures);
    }

    private async Task<bool> UpdateTeamAsync(Team team, CancellationToken cancellationToken)
    {
        return !string.IsNullOrWhiteSpace(team.Id)
            && await _teamsRepository.UpdateAsync(team.Id, team, cancellationToken);
    }
}
