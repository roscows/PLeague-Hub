using System.Linq.Expressions;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Tests;

public sealed class SearchEndpointsTests
{
    private const string ManchesterCityId = "665000000000000000000003";

    [Fact]
    public async Task SearchAsync_ReturnsPlayersAndTeams_UsingCaseInsensitivePrefixes()
    {
        var service = CreateService();

        var playerResults = await service.SearchAsync("eRL", 8);
        var teamResults = await service.SearchAsync("ar", 8);

        var player = Assert.Single(playerResults);
        Assert.Equal("player", player.Type);
        Assert.Equal("Erling Haaland", player.Name);
        Assert.Equal("Manchester City", player.Subtitle);

        var team = Assert.Single(teamResults);
        Assert.Equal("team", team.Type);
        Assert.Equal("Arsenal", team.Name);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNoResults_WhenQueryHasFewerThanTwoCharacters()
    {
        var service = CreateService();

        var results = await service.SearchAsync("a", 8);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_LimitsCombinedResults()
    {
        var teams = Enumerable.Range(1, 12)
            .Select(index => new Team
            {
                Id = $"6650000000000000000001{index:00}",
                Naziv = $"Manchester {index:00}"
            })
            .ToList();
        var service = new SearchService(
            new FakeRepository<Team>(teams),
            new FakeRepository<Player>([]));

        var results = await service.SearchAsync("man", 5);

        Assert.Equal(5, results.Count);
    }

    private static SearchService CreateService()
    {
        return new SearchService(
            new FakeRepository<Team>(
            [
                new Team
                {
                    Id = "665000000000000000000001",
                    Naziv = "Arsenal",
                    Skracenica = "ARS",
                    LogoUrl = "https://example.com/arsenal.svg"
                },
                new Team
                {
                    Id = ManchesterCityId,
                    Naziv = "Manchester City",
                    Skracenica = "MCI",
                    LogoUrl = "https://example.com/city.svg"
                }
            ]),
            new FakeRepository<Player>(
            [
                new Player
                {
                    Id = "665000000000000000000101",
                    TeamId = ManchesterCityId,
                    Ime = "Erling",
                    Prezime = "Haaland",
                    Pozicija = "Napadac"
                }
            ]));
    }

    private sealed class FakeRepository<TDocument> : IRepository<TDocument>
        where TDocument : BaseDocument
    {
        private readonly List<TDocument> _documents;

        public FakeRepository(IEnumerable<TDocument> documents)
        {
            _documents = documents.ToList();
        }

        public Task<IReadOnlyCollection<TDocument>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<TDocument>>(_documents);

        public Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_documents.FirstOrDefault(document => document.Id == id));

        public Task<TDocument?> FindOneAsync(
            Expression<Func<TDocument, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_documents.AsQueryable().FirstOrDefault(predicate));

        public Task<bool> ExistsAsync(
            Expression<Func<TDocument, bool>> predicate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_documents.AsQueryable().Any(predicate));

        public Task<TDocument> CreateAsync(TDocument document, CancellationToken cancellationToken = default) =>
            Task.FromResult(document);

        public Task<bool> UpdateAsync(string id, TDocument document, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
