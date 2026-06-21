using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PLeagueHub.Api.Configuration;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Data;

public sealed class MongoContext
{
    public MongoContext(IOptions<MongoDbSettings> options)
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException("MongoDB connection string is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
        {
            throw new InvalidOperationException("MongoDB database name is not configured.");
        }

        var client = new MongoClient(settings.ConnectionString);
        Database = client.GetDatabase(settings.DatabaseName);

        Teams = Database.GetCollection<Team>(settings.TeamsCollectionName);
        Players = Database.GetCollection<Player>(settings.PlayersCollectionName);
        Matches = Database.GetCollection<Match>(settings.MatchesCollectionName);
        Statistics = Database.GetCollection<Statistic>(settings.StatisticsCollectionName);
        Users = Database.GetCollection<User>(settings.UsersCollectionName);
        Posts = Database.GetCollection<Post>(settings.PostsCollectionName);
        Comments = Database.GetCollection<Comment>(settings.CommentsCollectionName);
        CommentVotes = Database.GetCollection<CommentVote>(settings.CommentVotesCollectionName);
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<Team> Teams { get; }

    public IMongoCollection<Player> Players { get; }

    public IMongoCollection<Match> Matches { get; }

    public IMongoCollection<Statistic> Statistics { get; }

    public IMongoCollection<User> Users { get; }

    public IMongoCollection<Post> Posts { get; }

    public IMongoCollection<Comment> Comments { get; }

    public IMongoCollection<CommentVote> CommentVotes { get; }

    public IMongoCollection<TDocument> GetCollection<TDocument>()
        where TDocument : BaseDocument
    {
        return typeof(TDocument) switch
        {
            var type when type == typeof(Team) => (IMongoCollection<TDocument>)Teams,
            var type when type == typeof(Player) => (IMongoCollection<TDocument>)Players,
            var type when type == typeof(Match) => (IMongoCollection<TDocument>)Matches,
            var type when type == typeof(Statistic) => (IMongoCollection<TDocument>)Statistics,
            var type when type == typeof(User) => (IMongoCollection<TDocument>)Users,
            var type when type == typeof(Post) => (IMongoCollection<TDocument>)Posts,
            var type when type == typeof(Comment) => (IMongoCollection<TDocument>)Comments,
            var type when type == typeof(CommentVote) => (IMongoCollection<TDocument>)CommentVotes,
            _ => throw new InvalidOperationException(
                $"Mongo collection is not configured for document type {typeof(TDocument).Name}.")
        };
    }
}
