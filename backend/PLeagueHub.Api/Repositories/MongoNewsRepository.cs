using MongoDB.Bson;
using MongoDB.Driver;
using PLeagueHub.Api.Data;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Repositories;

public sealed class MongoNewsRepository : INewsRepository
{
    private readonly MongoContext _context;

    public MongoNewsRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Post>> GetTimelineAsync(
        NewsQuery query,
        CancellationToken cancellationToken = default)
    {
        var posts = Builders<Post>.Filter;
        var filter = posts.Eq(post => post.Tip, "vest")
            & posts.Eq(post => post.Obrisan, false);

        if (!string.IsNullOrWhiteSpace(query.Category))
            filter &= posts.Eq(post => post.Kategorija, query.Category);
        if (!string.IsNullOrWhiteSpace(query.Reliability))
            filter &= posts.Eq(post => post.Pouzdanost, query.Reliability);
        if (!string.IsNullOrWhiteSpace(query.SourceId))
            filter &= posts.Eq(post => post.SourceId, query.SourceId);

        if (query.BeforePublishedAt.HasValue && !string.IsNullOrWhiteSpace(query.BeforeId))
        {
            filter &= posts.Lt(post => post.PublishedAt, query.BeforePublishedAt.Value)
                | (posts.Eq(post => post.PublishedAt, query.BeforePublishedAt.Value)
                   & posts.Lt(post => post.Id, query.BeforeId));
        }

        return await _context.Posts
            .Find(filter)
            .SortByDescending(post => post.PublishedAt)
            .ThenByDescending(post => post.Id)
            .Limit(Math.Clamp(query.Limit, 1, 51))
            .ToListAsync(cancellationToken);
    }

    public Task<Post?> GetVisibleAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(id, out _)) return Task.FromResult<Post?>(null);
        return _context.Posts
            .Find(post => post.Id == id && post.Tip == "vest" && !post.Obrisan)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<Post?> FindDuplicateAsync(
        string? externalId,
        string originalUrl,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        var posts = Builders<Post>.Filter;
        var duplicate = posts.Eq(post => post.OriginalUrl, originalUrl)
            | posts.Eq(post => post.Fingerprint, fingerprint);
        if (!string.IsNullOrWhiteSpace(externalId))
            duplicate |= posts.Eq(post => post.ExternalId, externalId);

        return await _context.Posts
            .Find(posts.Eq(post => post.Tip, "vest")
                & posts.Eq(post => post.Obrisan, false)
                & duplicate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Post> CreateAsync(Post post, CancellationToken cancellationToken = default)
    {
        await _context.Posts.InsertOneAsync(post, cancellationToken: cancellationToken);
        return post;
    }

    public async Task<bool> UpdateAsync(
        string id,
        Post post,
        CancellationToken cancellationToken = default)
    {
        post.Id = id;
        var result = await _context.Posts.ReplaceOneAsync(
            existing => existing.Id == id,
            post,
            cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public async Task<bool> PromoteRumorAsync(
        string id,
        Post officialPost,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<Post>.Update
            .Set(post => post.SourceId, officialPost.SourceId)
            .Set(post => post.OriginalUrl, officialPost.OriginalUrl)
            .Set(post => post.ExternalId, officialPost.ExternalId)
            .Set(post => post.ExternalAuthor, officialPost.ExternalAuthor)
            .Set(post => post.Pouzdanost, "zvanicno")
            .Set(post => post.PublishedAt, officialPost.PublishedAt)
            .Set(post => post.ImageUrl, officialPost.ImageUrl)
            .Set(post => post.UpdatedAt, officialPost.UpdatedAt);
        var result = await _context.Posts.UpdateOneAsync(
            post => post.Id == id && post.Pouzdanost == "glasina",
            update,
            cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<IReadOnlyList<NewsSource>> GetDueSourcesAsync(
        DateTime dueBefore,
        CancellationToken cancellationToken = default) =>
        await _context.NewsSources
            .Find(source => source.Aktivan
                && source.PauziranRazlog == null
                && (source.PoslednjaProveraAt == null || source.PoslednjaProveraAt <= dueBefore))
            .SortBy(source => source.PoslednjaProveraAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NewsSource>> GetSourcesAsync(
        CancellationToken cancellationToken = default) =>
        await _context.NewsSources.Find(_ => true).SortBy(source => source.Naziv).ToListAsync(cancellationToken);

    public Task<NewsSource?> GetSourceAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(id, out _)) return Task.FromResult<NewsSource?>(null);
        return _context.NewsSources.Find(source => source.Id == id).FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<NewsSource> CreateSourceAsync(
        NewsSource source,
        CancellationToken cancellationToken = default)
    {
        await _context.NewsSources.InsertOneAsync(source, cancellationToken: cancellationToken);
        return source;
    }

    public async Task<bool> UpdateSourceAsync(
        string id,
        NewsSource source,
        CancellationToken cancellationToken = default)
    {
        source.Id = id;
        var result = await _context.NewsSources.ReplaceOneAsync(
            existing => existing.Id == id,
            source,
            cancellationToken: cancellationToken);
        return result.MatchedCount > 0;
    }

    public Task MarkSourceSuccessAsync(
        string id,
        DateTime checkedAt,
        string? etag,
        DateTimeOffset? lastModified,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<NewsSource>.Update
            .Set(source => source.UzastopneGreske, 0)
            .Set(source => source.PoslednjaProveraAt, checkedAt)
            .Set(source => source.PoslednjiUspehAt, checkedAt)
            .Set(source => source.Etag, etag)
            .Set(source => source.LastModified, lastModified)
            .Set(source => source.UpdatedAt, checkedAt);
        return _context.NewsSources.UpdateOneAsync(
            source => source.Id == id,
            update,
            cancellationToken: cancellationToken);
    }

    public Task MarkSourceFailureAsync(
        string id,
        DateTime checkedAt,
        string reason,
        int pauseAfter,
        CancellationToken cancellationToken = default)
    {
        var pauseReason = $"Automatski pauziran nakon tri uzastopne greske. Poslednja greska: {reason}";
        var update = new[]
        {
            new MongoDB.Bson.BsonDocument("$set", new MongoDB.Bson.BsonDocument
            {
                ["poslednjaProveraAt"] = checkedAt,
                ["uzastopneGreske"] = new MongoDB.Bson.BsonDocument("$add", new MongoDB.Bson.BsonArray
                {
                    new MongoDB.Bson.BsonDocument("$ifNull", new MongoDB.Bson.BsonArray { "$uzastopneGreske", 0 }),
                    1
                }),
                ["updatedAt"] = checkedAt
            }),
            new MongoDB.Bson.BsonDocument("$set", new MongoDB.Bson.BsonDocument
            {
                ["aktivan"] = new MongoDB.Bson.BsonDocument("$cond", new MongoDB.Bson.BsonArray
                {
                    new MongoDB.Bson.BsonDocument("$gte", new MongoDB.Bson.BsonArray { "$uzastopneGreske", pauseAfter }),
                    false,
                    "$aktivan"
                }),
                ["pauziranRazlog"] = new MongoDB.Bson.BsonDocument("$cond", new MongoDB.Bson.BsonArray
                {
                    new MongoDB.Bson.BsonDocument("$gte", new MongoDB.Bson.BsonArray { "$uzastopneGreske", pauseAfter }),
                    pauseReason,
                    "$pauziranRazlog"
                })
            })
        };
        return _context.NewsSources.UpdateOneAsync(
            source => source.Id == id,
            new PipelineUpdateDefinition<NewsSource>(update),
            cancellationToken: cancellationToken);
    }

    public Task RecordAuditAsync(
        EditorialAuditEvent auditEvent,
        CancellationToken cancellationToken = default) =>
        _context.EditorialAuditEvents.InsertOneAsync(auditEvent, cancellationToken: cancellationToken);
}
