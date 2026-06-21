using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using PLeagueHub.Api.Data;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Repositories;

public sealed class MongoForumRepository : IForumRepository
{
    private readonly MongoContext _context;

    public MongoForumRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<ForumTopicPage> GetTopicsAsync(
        ForumQuery query,
        CancellationToken cancellationToken = default)
    {
        var filter = VisibleDiscussionFilter();

        if (query.Search is not null)
        {
            var pattern = new BsonRegularExpression(Regex.Escape(query.Search), "i");
            filter &= Builders<Post>.Filter.Regex(post => post.Naslov, pattern);
        }

        var total = await _context.Posts.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.Posts
            .Find(filter)
            .SortByDescending(post => post.Istaknut)
            .ThenByDescending(post => post.PoslednjaAktivnost)
            .ThenByDescending(post => post.DatumKreiranja)
            .ThenByDescending(post => post.Id)
            .Skip(query.Skip)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);

        return new ForumTopicPage(items, total);
    }

    public async Task<Post?> GetVisibleDiscussionAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _context.Posts
            .Find(VisibleDiscussionFilter() & Builders<Post>.Filter.Eq(post => post.Id, id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Post?> GetVisiblePostAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(id, out _)) return null;

        var posts = Builders<Post>.Filter;
        var filter = posts.Eq(post => post.Id, id)
            & posts.Eq(post => post.Obrisan, false)
            & posts.In(post => post.Tip, ["diskusija", "vest"]);
        return await _context.Posts.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Comment?> GetCommentAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(id, out _))
        {
            return null;
        }

        return await _context.Comments
            .Find(comment => comment.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Comment>> GetCommentsAsync(
        string postId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Comments
            .Find(comment => comment.PostId == postId)
            .SortBy(comment => comment.DatumKreiranja)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Comment>> GetCommentsForPostsAsync(
        IReadOnlyCollection<string> postIds,
        CancellationToken cancellationToken = default)
    {
        var ids = postIds
            .Where(id => ObjectId.TryParse(id, out _))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Comments
            .Find(Builders<Comment>.Filter.In(comment => comment.PostId, ids))
            .SortBy(comment => comment.DatumKreiranja)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, User>> GetUsersAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds
            .Where(id => ObjectId.TryParse(id, out _))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, User>();
        }

        var users = await _context.Users
            .Find(Builders<User>.Filter.In(user => user.Id, ids))
            .ToListAsync(cancellationToken);

        return users.Where(user => user.Id is not null).ToDictionary(user => user.Id!);
    }

    public async Task<IReadOnlyList<CommentVote>> GetVotesAsync(
        IReadOnlyCollection<string> commentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = commentIds
            .Where(id => ObjectId.TryParse(id, out _))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.CommentVotes
            .Find(Builders<CommentVote>.Filter.In(vote => vote.CommentId, ids))
            .ToListAsync(cancellationToken);
    }

    public async Task<Post> CreateDiscussionAsync(Post post, CancellationToken cancellationToken = default)
    {
        await _context.Posts.InsertOneAsync(post, cancellationToken: cancellationToken);
        return post;
    }

    public async Task<Comment> CreateCommentAsync(
        Comment comment,
        CancellationToken cancellationToken = default)
    {
        await _context.Comments.InsertOneAsync(comment, cancellationToken: cancellationToken);
        await _context.Posts.UpdateOneAsync(
            post => post.Id == comment.PostId,
            Builders<Post>.Update.Set(post => post.PoslednjaAktivnost, comment.DatumKreiranja),
            cancellationToken: cancellationToken);
        return comment;
    }

    public async Task<CommentVote?> GetVoteAsync(
        string commentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CommentVotes
            .Find(vote => vote.CommentId == commentId && vote.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertVoteAsync(CommentVote vote, CancellationToken cancellationToken = default)
    {
        var filter = Builders<CommentVote>.Filter.Where(
            existing => existing.CommentId == vote.CommentId && existing.UserId == vote.UserId);
        var update = Builders<CommentVote>.Update
            .Set(existing => existing.Value, vote.Value)
            .Set(existing => existing.UpdatedAt, vote.UpdatedAt)
            .SetOnInsert(existing => existing.Id, vote.Id ?? ObjectId.GenerateNewId().ToString())
            .SetOnInsert(existing => existing.CommentId, vote.CommentId)
            .SetOnInsert(existing => existing.UserId, vote.UserId)
            .SetOnInsert(existing => existing.CreatedAt, vote.CreatedAt);

        await _context.CommentVotes.UpdateOneAsync(
            filter,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<bool> DeleteVoteAsync(
        string commentId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var result = await _context.CommentVotes.DeleteOneAsync(
            vote => vote.CommentId == commentId && vote.UserId == userId,
            cancellationToken);
        return result.DeletedCount > 0;
    }

    private static FilterDefinition<Post> VisibleDiscussionFilter()
    {
        return Builders<Post>.Filter.Eq(post => post.Tip, "diskusija")
            & Builders<Post>.Filter.Eq(post => post.Obrisan, false);
    }
}
