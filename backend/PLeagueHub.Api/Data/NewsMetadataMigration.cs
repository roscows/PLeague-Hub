using MongoDB.Bson;
using MongoDB.Driver;

namespace PLeagueHub.Api.Data;

public sealed class NewsMetadataMigration
{
    private readonly MongoContext _context;

    public NewsMetadataMigration(MongoContext context)
    {
        _context = context;
    }

    public async Task<long> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var filter = new BsonDocument("tip", "vest");
        var update = new PipelineUpdateDefinition<Models.Post>(new[]
        {
            new BsonDocument("$set", new BsonDocument
            {
                ["publishedAt"] = new BsonDocument("$ifNull", new BsonArray { "$publishedAt", "$datumKreiranja" }),
                ["fetchedAt"] = new BsonDocument("$ifNull", new BsonArray { "$fetchedAt", "$datumKreiranja" }),
                ["kategorija"] = new BsonDocument("$ifNull", new BsonArray { "$kategorija", "premier_league" }),
                ["pouzdanost"] = new BsonDocument("$ifNull", new BsonArray { "$pouzdanost", "pouzdan_izvor" }),
                ["uvozAutomatski"] = new BsonDocument("$ifNull", new BsonArray { "$uvozAutomatski", false }),
                ["updatedAt"] = new BsonDocument("$ifNull", new BsonArray { "$updatedAt", "$datumKreiranja" }),
                ["originalUrl"] = RemoveIdentityWhenDeleted("$originalUrl"),
                ["xEmbedUrl"] = RemoveIdentityWhenDeleted("$xEmbedUrl"),
                ["externalId"] = RemoveIdentityWhenDeleted("$externalId"),
                ["fingerprint"] = RemoveIdentityWhenDeleted("$fingerprint")
            })
        });

        var result = await _context.Posts.UpdateManyAsync(
            filter,
            update,
            cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    private static BsonDocument RemoveIdentityWhenDeleted(string field) =>
        new("$cond", new BsonArray
        {
            new BsonDocument("$eq", new BsonArray { "$obrisan", true }),
            "$$REMOVE",
            new BsonDocument("$ifNull", new BsonArray { field, "$$REMOVE" })
        });
}
