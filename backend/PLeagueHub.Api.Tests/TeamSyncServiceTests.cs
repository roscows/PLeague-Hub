using System.Linq.Expressions;
using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class TeamSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_MatchesSeedTeamByNameAndPreservesProfileFields()
    {
        var existing = Team("665000000000000000000001", "  arsenal  ");
        existing.Skracenica = "OLD";
        existing.Stadion = "Emirates Stadium";
        existing.Osnovan = 1886;
        existing.LogoUrl = "arsenal.svg";
        var repository = new FakeTeamRepository([existing]);
        var service = CreateService(repository,
            [new FootballTeamStanding(42, "Arsenal", "ARS", 1, 85)]);

        var result = await service.SyncAsync(76986);

        Assert.Equal(1, result.Updated);
        Assert.Equal(42, existing.ProviderId);
        Assert.Equal("Arsenal", existing.Naziv);
        Assert.Equal("ARS", existing.Skracenica);
        Assert.Equal("Emirates Stadium", existing.Stadion);
        Assert.Equal(1886, existing.Osnovan);
        Assert.Equal("arsenal.svg", existing.LogoUrl);
        Assert.Equal(85, existing.Bodovi);
        Assert.Equal(1, existing.Pozicija);
    }

    [Fact]
    public async Task SyncAsync_MatchesByProviderIdBeforeName()
    {
        var providerMatch = Team("665000000000000000000001", "Old Arsenal Name", 42);
        var nameMatch = Team("665000000000000000000002", "Arsenal");
        var repository = new FakeTeamRepository([providerMatch, nameMatch]);
        var service = CreateService(repository,
            [new FootballTeamStanding(42, "Arsenal", "ARS", 1, 85)]);

        await service.SyncAsync(76986);

        Assert.Equal("Arsenal", providerMatch.Naziv);
        Assert.Null(nameMatch.ProviderId);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public async Task SyncAsync_MatchesSeedTeamByAbbreviation_WhenProviderAddsNameSuffix()
    {
        var existing = Team("665000000000000000000002", "Liverpool");
        existing.Skracenica = "LIV";
        var repository = new FakeTeamRepository([existing]);
        var service = CreateService(repository,
            [new FootballTeamStanding(44, "Liverpool FC", "LIV", 5, 70)]);

        var result = await service.SyncAsync(76986);

        Assert.Equal(1, result.Updated);
        Assert.Equal(44, existing.ProviderId);
        Assert.Equal("Liverpool FC", existing.Naziv);
        Assert.Single(repository.Documents);
    }

    [Fact]
    public async Task SyncAsync_CreatesMissingTeam()
    {
        var repository = new FakeTeamRepository([]);
        var service = CreateService(repository,
            [new FootballTeamStanding(50, "Liverpool", "LIV", 2, 84)]);

        var result = await service.SyncAsync(76986);

        Assert.Equal(1, result.Created);
        var created = Assert.Single(repository.Documents);
        Assert.Equal(50, created.ProviderId);
        Assert.Equal("Liverpool", created.Naziv);
        Assert.Equal(string.Empty, created.Stadion);
        Assert.Equal(string.Empty, created.LogoUrl);
        Assert.Equal(0, created.Osnovan);
    }

    [Fact]
    public async Task SyncAsync_SkipsUnchangedTeam()
    {
        var existing = Team("665000000000000000000001", "Arsenal", 42);
        existing.Skracenica = "ARS";
        existing.Pozicija = 1;
        existing.Bodovi = 85;
        var repository = new FakeTeamRepository([existing]);
        var service = CreateService(repository,
            [new FootballTeamStanding(42, "Arsenal", "ARS", 1, 85)]);

        var result = await service.SyncAsync(76986);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, repository.UpdateCount);
    }

    [Fact]
    public async Task SyncAsync_RejectsEmptyStandingsWithoutWrites()
    {
        var repository = new FakeTeamRepository([]);
        var service = CreateService(repository, []);

        await Assert.ThrowsAsync<TeamSyncException>(() => service.SyncAsync(76986));

        Assert.Equal(0, repository.WriteCount);
    }

    [Fact]
    public async Task SyncAsync_RejectsDuplicateProviderIdsWithoutWrites()
    {
        var repository = new FakeTeamRepository([]);
        var service = CreateService(repository,
        [
            new FootballTeamStanding(42, "Arsenal", "ARS", 1, 85),
            new FootballTeamStanding(42, "Another Arsenal", "AAR", 2, 80)
        ]);

        await Assert.ThrowsAsync<TeamSyncException>(() => service.SyncAsync(76986));

        Assert.Equal(0, repository.WriteCount);
    }

    private static TeamSyncService CreateService(
        FakeTeamRepository repository,
        IReadOnlyCollection<FootballTeamStanding> standings)
    {
        return new TeamSyncService(new FakeFootballProvider(standings), repository);
    }

    private static Team Team(string id, string name, int? providerId = null)
    {
        return new Team
        {
            Id = id,
            ProviderId = providerId,
            Naziv = name,
            Skracenica = "OLD",
            Stadion = "Existing stadium",
            Osnovan = 1900,
            LogoUrl = "existing.svg",
            Bodovi = 1,
            Pozicija = 20
        };
    }

    private sealed class FakeFootballProvider(
        IReadOnlyCollection<FootballTeamStanding> standings) : IFootballProvider
    {
        public Task<FootballTeamLogo> GetTeamLogoAsync(
            int providerId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(
            int tournamentId,
            int seasonId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(standings);
        }

        public Task<JsonDocument> SearchAsync(
            string term,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeTeamRepository(IEnumerable<Team> teams) : IRepository<Team>
    {
        public List<Team> Documents { get; } = teams.ToList();

        public int CreateCount { get; private set; }

        public int UpdateCount { get; private set; }

        public int WriteCount => CreateCount + UpdateCount;

        public Task<IReadOnlyCollection<Team>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<Team>>(Documents);
        }

        public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Documents.FirstOrDefault(team => team.Id == id));
        }

        public Task<Team?> FindOneAsync(
            Expression<Func<Team, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Documents.AsQueryable().FirstOrDefault(predicate));
        }

        public Task<bool> ExistsAsync(
            Expression<Func<Team, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Documents.AsQueryable().Any(predicate));
        }

        public Task<Team> CreateAsync(Team document, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            document.Id ??= $"665000000000000000000{Documents.Count + 100:D3}";
            Documents.Add(document);
            return Task.FromResult(document);
        }

        public Task<bool> UpdateAsync(
            string id,
            Team document,
            CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            return Task.FromResult(Documents.Any(team => team.Id == id));
        }

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
