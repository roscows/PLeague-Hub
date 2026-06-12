using System.Linq.Expressions;
using PLeagueHub.Api.Data.Seeding;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Tests;

public sealed class DatabaseSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesInitialSportsData_WhenCollectionsAreEmpty()
    {
        var teams = new FakeRepository<Team>();
        var players = new FakeRepository<Player>();
        var matches = new FakeRepository<Match>();
        var statistics = new FakeRepository<Statistic>();
        var posts = new FakeRepository<Post>();

        var seeder = new DatabaseSeeder(teams, players, matches, statistics, posts);

        await seeder.SeedAsync();

        Assert.True(teams.Documents.Count >= 6);
        Assert.True(players.Documents.Count >= 6);
        Assert.True(matches.Documents.Count >= 3);
        Assert.True(statistics.Documents.Count >= 3);
        Assert.True(posts.Documents.Count >= 2);
    }

    [Fact]
    public async Task SeedAsync_DoesNotDuplicateData_WhenRunMoreThanOnce()
    {
        var teams = new FakeRepository<Team>();
        var players = new FakeRepository<Player>();
        var matches = new FakeRepository<Match>();
        var statistics = new FakeRepository<Statistic>();
        var posts = new FakeRepository<Post>();

        var seeder = new DatabaseSeeder(teams, players, matches, statistics, posts);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(6, teams.Documents.Count);
        Assert.Equal(6, players.Documents.Count);
        Assert.Equal(3, matches.Documents.Count);
        Assert.Equal(3, statistics.Documents.Count);
        Assert.Equal(2, posts.Documents.Count);
    }

    private sealed class FakeRepository<TDocument> : IRepository<TDocument>
        where TDocument : BaseDocument
    {
        public List<TDocument> Documents { get; } = [];

        public Task<IReadOnlyCollection<TDocument>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<TDocument>>(Documents);
        }

        public Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Documents.FirstOrDefault(document => document.Id == id));
        }

        public Task<TDocument?> FindOneAsync(
            Expression<Func<TDocument, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Documents.AsQueryable().FirstOrDefault(predicate));
        }

        public Task<bool> ExistsAsync(
            Expression<Func<TDocument, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Documents.AsQueryable().Any(predicate));
        }

        public Task<TDocument> CreateAsync(TDocument document, CancellationToken cancellationToken = default)
        {
            Documents.Add(document);
            return Task.FromResult(document);
        }

        public Task<bool> UpdateAsync(string id, TDocument document, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
