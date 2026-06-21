using System.Linq.Expressions;
using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class TeamLogoSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_DownloadsCachesAndUpdatesMissingLogo()
    {
        var team = CreateTeam("team-1", 60, string.Empty);
        var repository = new FakeTeamRepository([team]);
        var provider = new FakeFootballProvider();
        var cache = new FakeTeamLogoCache();
        var pacer = new FakeProviderRequestPacer();
        var service = new TeamLogoSyncService(provider, repository, cache, pacer);

        var result = await service.SyncAsync();

        Assert.Equal(1, result.Downloaded);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Failed);
        Assert.Equal("/team-logos/60.png", team.LogoUrl);
        Assert.Equal(1, provider.LogoRequestCount);
        Assert.Equal(1, pacer.WaitCount);
        Assert.Contains(60, cache.ProviderIds);
    }

    [Fact]
    public async Task SyncAsync_SkipsCachedLogoWithoutProviderRequest()
    {
        var team = CreateTeam("team-1", 60, "/team-logos/60.png");
        var repository = new FakeTeamRepository([team]);
        var provider = new FakeFootballProvider();
        var cache = new FakeTeamLogoCache([60]);
        var pacer = new FakeProviderRequestPacer();
        var service = new TeamLogoSyncService(provider, repository, cache, pacer);

        var result = await service.SyncAsync();

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, provider.LogoRequestCount);
        Assert.Equal(0, pacer.WaitCount);
    }

    [Fact]
    public async Task SyncAsync_RepairsUrlForExistingCacheFile()
    {
        var team = CreateTeam("team-1", 60, "https://old.example/logo.png");
        var repository = new FakeTeamRepository([team]);
        var provider = new FakeFootballProvider();
        var cache = new FakeTeamLogoCache([60]);
        var service = new TeamLogoSyncService(
            provider,
            repository,
            cache,
            new FakeProviderRequestPacer());

        var result = await service.SyncAsync();

        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, result.Updated);
        Assert.Equal("/team-logos/60.png", team.LogoUrl);
        Assert.Equal(0, provider.LogoRequestCount);
    }

    [Fact]
    public async Task SyncAsync_ContinuesAfterSingleProviderFailure()
    {
        var repository = new FakeTeamRepository(
        [
            CreateTeam("team-1", 60, string.Empty),
            CreateTeam("team-2", 42, string.Empty)
        ]);
        var provider = new FakeFootballProvider(failingProviderIds: [60]);
        var cache = new FakeTeamLogoCache();
        var service = new TeamLogoSyncService(
            provider,
            repository,
            cache,
            new FakeProviderRequestPacer());

        var result = await service.SyncAsync();

        Assert.Equal(1, result.Downloaded);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Failed);
        Assert.Contains(60, result.FailedProviderIds);
        Assert.Contains(42, cache.ProviderIds);
    }

    private static Team CreateTeam(string id, int providerId, string logoUrl)
    {
        return new Team
        {
            Id = id,
            ProviderId = providerId,
            Naziv = $"Team {providerId}",
            Skracenica = $"T{providerId}",
            LogoUrl = logoUrl
        };
    }

    private sealed class FakeFootballProvider(
        IEnumerable<int>? failingProviderIds = null) : IFootballProvider
    {
        private readonly HashSet<int> _failingProviderIds = failingProviderIds?.ToHashSet() ?? [];

        public int LogoRequestCount { get; private set; }

        public Task<FootballTeamLogo> GetTeamLogoAsync(
            int providerId,
            CancellationToken cancellationToken = default)
        {
            LogoRequestCount++;

            if (_failingProviderIds.Contains(providerId))
            {
                throw new HttpRequestException("Provider failure.");
            }

            return Task.FromResult(new FootballTeamLogo([137, 80, 78, 71], "image/png"));
        }

        public Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(
            int tournamentId,
            int seasonId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<JsonDocument> SearchAsync(
            string term,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTeamLogoCache(IEnumerable<int>? existingProviderIds = null) : ITeamLogoCache
    {
        public HashSet<int> ProviderIds { get; } = existingProviderIds?.ToHashSet() ?? [];

        public bool Exists(int providerId) => ProviderIds.Contains(providerId);

        public string GetPublicUrl(int providerId) => $"/team-logos/{providerId}.png";

        public Task SaveAsync(
            int providerId,
            FootballTeamLogo logo,
            CancellationToken cancellationToken = default)
        {
            ProviderIds.Add(providerId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProviderRequestPacer : IProviderRequestPacer
    {
        public int WaitCount { get; private set; }

        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            WaitCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTeamRepository(IEnumerable<Team> teams) : IRepository<Team>
    {
        private readonly List<Team> _teams = teams.ToList();

        public Task<IReadOnlyCollection<Team>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<Team>>(_teams);

        public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.FirstOrDefault(team => team.Id == id));

        public Task<Team?> FindOneAsync(
            Expression<Func<Team, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.AsQueryable().FirstOrDefault(predicate));

        public Task<bool> ExistsAsync(
            Expression<Func<Team, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.AsQueryable().Any(predicate));

        public Task<Team> CreateAsync(Team document, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> UpdateAsync(
            string id,
            Team document,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_teams.Any(team => team.Id == id));

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
