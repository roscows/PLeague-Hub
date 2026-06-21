using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using PLeagueHub.Api.Data;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Repositories;

public sealed class MongoRepository<TDocument> : IRepository<TDocument>
    where TDocument : BaseDocument
{
    private readonly IMongoCollection<TDocument> _collection;

    public MongoRepository(MongoContext context)
    {
        _collection = context.GetCollection<TDocument>();
    }

    public async Task<IReadOnlyCollection<TDocument>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(_ => true)
            .ToListAsync(cancellationToken);
    }

    public async Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!IsValidObjectId(id))
        {
            return null;
        }

        return await _collection
            .Find(document => document.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TDocument?> FindOneAsync(
        Expression<Func<TDocument, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(predicate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<TDocument, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _collection
            .Find(predicate)
            .AnyAsync(cancellationToken);
    }

    public async Task<TDocument> CreateAsync(TDocument document, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        return document;
    }

    public async Task<bool> UpdateAsync(string id, TDocument document, CancellationToken cancellationToken = default)
    {
        if (!IsValidObjectId(id))
        {
            return false;
        }

        document.Id = id;

        var result = await _collection.ReplaceOneAsync(
            existing => existing.Id == id,
            document,
            cancellationToken: cancellationToken);

        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!IsValidObjectId(id))
        {
            return false;
        }

        var result = await _collection.DeleteOneAsync(
            document => document.Id == id,
            cancellationToken);

        return result.IsAcknowledged && result.DeletedCount > 0;
    }

    private static bool IsValidObjectId(string id)
    {
        return ObjectId.TryParse(id, out _);
    }
}
