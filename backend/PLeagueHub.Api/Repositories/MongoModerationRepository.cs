using MongoDB.Bson;
using MongoDB.Driver;
using PLeagueHub.Api.Data;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Repositories;

public sealed class MongoModerationRepository : IModerationRepository
{
    private readonly MongoContext _context;

    public MongoModerationRepository(MongoContext context)
    {
        _context = context;
    }

    public Task<User?> GetUserAsync(string id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(_context.Users, id, cancellationToken);

    public Task<Post?> GetPostAsync(string id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(_context.Posts, id, cancellationToken);

    public Task<Comment?> GetCommentAsync(string id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(_context.Comments, id, cancellationToken);

    public async Task<bool> SetActiveModerationAsync(
        string userId,
        ActiveModeration? state,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(userId, out _)) return false;

        var update = state is null
            ? Builders<User>.Update.Unset(user => user.AktivnaModeracija)
            : Builders<User>.Update.Set(user => user.AktivnaModeracija, state);
        var result = await _context.Users.UpdateOneAsync(
            user => user.Id == userId,
            update,
            cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }

    public async Task<bool> ExpireModerationAsync(
        string userId,
        DateTime expectedStart,
        ModerationAction action,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(userId, out _)) return false;

        var filter = Builders<User>.Filter.And(
            Builders<User>.Filter.Eq(user => user.Id, userId),
            Builders<User>.Filter.Eq("aktivnaModeracija.pocetak", expectedStart));
        var expiredUser = await _context.Users.FindOneAndUpdateAsync(
            filter,
            Builders<User>.Update.Unset(user => user.AktivnaModeracija),
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.Before },
            cancellationToken);

        if (expiredUser is null) return false;

        await _context.ModerationActions.InsertOneAsync(action, cancellationToken: cancellationToken);
        return true;
    }

    public Task CreateActionAsync(ModerationAction action, CancellationToken cancellationToken = default) =>
        _context.ModerationActions.InsertOneAsync(action, cancellationToken: cancellationToken);

    public Task<bool> SetPostDeletedAsync(string id, CancellationToken cancellationToken = default) =>
        UpdateByIdAsync(
            _context.Posts,
            id,
            Builders<Post>.Update.Set(post => post.Obrisan, true),
            cancellationToken);

    public Task<bool> SetCommentDeletedAsync(string id, CancellationToken cancellationToken = default) =>
        UpdateByIdAsync(
            _context.Comments,
            id,
            Builders<Comment>.Update.Set(comment => comment.Obrisan, true),
            cancellationToken);

    public Task<bool> SetPostPinnedAsync(
        string id,
        bool pinned,
        string moderatorId,
        DateTime now,
        CancellationToken cancellationToken = default) =>
        UpdateByIdAsync(
            _context.Posts,
            id,
            Builders<Post>.Update.Set(post => post.Istaknut, pinned),
            cancellationToken);

    public Task<bool> SetCommentPinnedAsync(
        string id,
        bool pinned,
        string moderatorId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var update = pinned
            ? Builders<Comment>.Update
                .Set(comment => comment.Istaknut, true)
                .Set(comment => comment.IstaknutAt, now)
                .Set(comment => comment.IstakaoId, moderatorId)
            : Builders<Comment>.Update
                .Set(comment => comment.Istaknut, false)
                .Unset(comment => comment.IstaknutAt)
                .Unset(comment => comment.IstakaoId);
        return UpdateByIdAsync(_context.Comments, id, update, cancellationToken);
    }

    private static async Task<TDocument?> FindByIdAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        string id,
        CancellationToken cancellationToken)
        where TDocument : BaseDocument
    {
        if (!ObjectId.TryParse(id, out _)) return null;
        return await collection.Find(document => document.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<bool> UpdateByIdAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        string id,
        UpdateDefinition<TDocument> update,
        CancellationToken cancellationToken)
        where TDocument : BaseDocument
    {
        if (!ObjectId.TryParse(id, out _)) return false;
        var result = await collection.UpdateOneAsync(
            document => document.Id == id,
            update,
            cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount > 0;
    }
}
