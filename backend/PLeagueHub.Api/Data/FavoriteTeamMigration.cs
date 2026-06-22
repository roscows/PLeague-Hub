using MongoDB.Bson;
using MongoDB.Driver;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Data;

public sealed class FavoriteTeamMigration
{
    private readonly MongoContext _context;

    public FavoriteTeamMigration(MongoContext context)
    {
        _context = context;
    }

    public async Task<long> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var favorites = new BsonDocument("$ifNull", new BsonArray
        {
            "$favoritniTimovi",
            new BsonArray()
        });
        var filter = new BsonDocument("$expr", new BsonDocument("$gt", new BsonArray
        {
            new BsonDocument("$size", favorites),
            1
        }));
        var update = new PipelineUpdateDefinition<User>(new[]
        {
            new BsonDocument("$set", new BsonDocument(
                "favoritniTimovi",
                new BsonDocument("$slice", new BsonArray { favorites, 1 })))
        });

        var result = await _context.Users.UpdateManyAsync(
            filter,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount;
    }
}
