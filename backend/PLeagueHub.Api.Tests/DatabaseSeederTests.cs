using System.Linq.Expressions;
using PLeagueHub.Api.Data.Seeding;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services;

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
        var comments = new FakeRepository<Comment>();
        var votes = new FakeRepository<CommentVote>();
        var users = new FakeRepository<User>();

        var seeder = new DatabaseSeeder(
            teams, players, matches, statistics, posts, comments, votes, users, new PasswordService());

        await seeder.SeedAsync();

        Assert.True(teams.Documents.Count >= 6);
        Assert.True(players.Documents.Count >= 6);
        Assert.True(matches.Documents.Count >= 3);
        Assert.True(statistics.Documents.Count >= 3);
        Assert.True(posts.Documents.Count >= 8);
        Assert.True(comments.Documents.Count >= 6);
        Assert.True(votes.Documents.Count >= 3);
        Assert.True(users.Documents.Count >= 3);
        Assert.Contains(posts.Documents, post => post.Tip == "diskusija" && post.Istaknut);
        Assert.Contains(comments.Documents, comment => comment.ParentCommentId is not null);
        Assert.All(votes.Documents, vote => Assert.True(vote.Value is 1 or -1));
        Assert.Contains(users.Documents, user => user.Email == "admin@pleaguehub.local" && user.Uloga == "administrator");
        Assert.Contains(users.Documents, user => user.Email == "moderator@pleaguehub.local" && user.Uloga == "moderator");
        Assert.All(users.Documents, user => Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash)));
    }

    [Fact]
    public async Task SeedAsync_DoesNotDuplicateData_WhenRunMoreThanOnce()
    {
        var teams = new FakeRepository<Team>();
        var players = new FakeRepository<Player>();
        var matches = new FakeRepository<Match>();
        var statistics = new FakeRepository<Statistic>();
        var posts = new FakeRepository<Post>();
        var comments = new FakeRepository<Comment>();
        var votes = new FakeRepository<CommentVote>();
        var users = new FakeRepository<User>();

        var seeder = new DatabaseSeeder(
            teams, players, matches, statistics, posts, comments, votes, users, new PasswordService());

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(6, teams.Documents.Count);
        Assert.Equal(6, players.Documents.Count);
        Assert.Equal(3, matches.Documents.Count);
        Assert.Equal(3, statistics.Documents.Count);
        Assert.Equal(10, posts.Documents.Count);
        Assert.Equal(7, comments.Documents.Count);
        Assert.Equal(4, votes.Documents.Count);
        Assert.Equal(3, users.Documents.Count);
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
