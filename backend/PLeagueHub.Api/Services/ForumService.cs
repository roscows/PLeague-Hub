using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public sealed class ForumService : IForumService
{
    private const string UnknownUser = "Nepoznat korisnik";
    private readonly IForumRepository _repository;
    private readonly IModerationService? _moderationService;

    public ForumService(IForumRepository repository)
        : this(repository, null)
    {
    }

    public ForumService(IForumRepository repository, IModerationService? moderationService)
    {
        _repository = repository;
        _moderationService = moderationService;
    }

    public async Task<PagedResponse<ForumTopicResponse>> GetTopicsAsync(
        ForumListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ForumQuery.Create(request.Search, request.Page, request.PageSize);
        var page = await _repository.GetTopicsAsync(query, cancellationToken);
        var postIds = page.Items.Select(post => post.Id!).ToList();
        var comments = await _repository.GetCommentsForPostsAsync(postIds, cancellationToken);
        var displayComments = SelectDisplayComments(comments);
        var userIds = page.Items.Select(post => post.AutorId)
            .Concat(displayComments.Select(comment => comment.AutorId))
            .Distinct()
            .ToList();
        var users = await _repository.GetUsersAsync(userIds, cancellationToken);

        var items = page.Items.Select(post =>
        {
            var postComments = displayComments
                .Where(comment => comment.PostId == post.Id)
                .OrderBy(comment => comment.DatumKreiranja)
                .ToList();
            var latestComment = postComments.LastOrDefault(comment => !comment.Obrisan);
            var latestActivity = latestComment?.DatumKreiranja
                ?? (post.PoslednjaAktivnost > DateTime.MinValue ? post.PoslednjaAktivnost : post.DatumKreiranja);

            return new ForumTopicResponse(
                post.Id!,
                post.Naslov,
                post.AutorId,
                Username(users, post.AutorId),
                postComments.Count,
                post.DatumKreiranja,
                latestActivity,
                latestComment is null ? Username(users, post.AutorId) : Username(users, latestComment.AutorId),
                post.Istaknut);
        }).ToList();

        var totalPages = page.Total == 0 ? 0 : (int)Math.Ceiling(page.Total / (double)query.PageSize);
        return new PagedResponse<ForumTopicResponse>(items, query.Page, query.PageSize, page.Total, totalPages);
    }

    public async Task<ForumDiscussionResponse?> GetDiscussionAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var post = await _repository.GetVisibleDiscussionAsync(id, cancellationToken);

        if (post is null)
        {
            return null;
        }

        var users = await _repository.GetUsersAsync([post.AutorId], cancellationToken);
        return MapDiscussion(post, users);
    }

    public async Task<IReadOnlyList<ForumCommentResponse>?> GetCommentsAsync(
        string postId,
        string? currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (await _repository.GetVisibleDiscussionAsync(postId, cancellationToken) is null)
        {
            return null;
        }

        var comments = SelectDisplayComments(await _repository.GetCommentsAsync(postId, cancellationToken));
        var users = await _repository.GetUsersAsync(
            comments.Select(comment => comment.AutorId).Distinct().ToList(),
            cancellationToken);
        var votes = await _repository.GetVotesAsync(
            comments.Select(comment => comment.Id!).ToList(),
            cancellationToken);

        return comments
            .Select((comment, index) => MapComment(comment, index + 1, users, votes, currentUserId))
            .ToList();
    }

    public async Task<ForumResult<ForumDiscussionResponse>> CreateDiscussionAsync(
        CreateForumPostRequest request,
        string? authorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorId))
        {
            return ForumResult<ForumDiscussionResponse>.Failure(ForumError.Unauthorized, "Prijava je obavezna.");
        }

        var writeAccess = await CheckWriteAccessAsync(authorId, cancellationToken);
        if (writeAccess is not null) return ForumResult<ForumDiscussionResponse>.Failure(ForumError.Forbidden, writeAccess);

        if (string.IsNullOrWhiteSpace(request.Naslov) || string.IsNullOrWhiteSpace(request.Sadrzaj))
        {
            return ForumResult<ForumDiscussionResponse>.Failure(ForumError.Validation, "Naslov i sadrzaj su obavezni.");
        }

        var now = DateTime.UtcNow;
        var post = await _repository.CreateDiscussionAsync(new Post
        {
            AutorId = authorId,
            Naslov = request.Naslov.Trim(),
            Sadrzaj = request.Sadrzaj.Trim(),
            Tip = "diskusija",
            DatumKreiranja = now,
            PoslednjaAktivnost = now
        }, cancellationToken);
        var users = await _repository.GetUsersAsync([authorId], cancellationToken);
        return ForumResult<ForumDiscussionResponse>.Success(MapDiscussion(post, users));
    }

    public async Task<ForumResult<ForumCommentResponse>> CreateCommentAsync(
        string postId,
        CreateCommentRequest request,
        string? authorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorId))
        {
            return ForumResult<ForumCommentResponse>.Failure(ForumError.Unauthorized, "Prijava je obavezna.");
        }

        var writeAccess = await CheckWriteAccessAsync(authorId, cancellationToken);
        if (writeAccess is not null) return ForumResult<ForumCommentResponse>.Failure(ForumError.Forbidden, writeAccess);

        if (string.IsNullOrWhiteSpace(request.Tekst))
        {
            return ForumResult<ForumCommentResponse>.Failure(ForumError.Validation, "Tekst komentara je obavezan.");
        }

        if (await _repository.GetVisibleDiscussionAsync(postId, cancellationToken) is null)
        {
            return ForumResult<ForumCommentResponse>.Failure(ForumError.NotFound, "Diskusija nije pronadjena.");
        }

        if (request.ParentCommentId is not null)
        {
            var parent = await _repository.GetCommentAsync(request.ParentCommentId, cancellationToken);

            if (parent is null || parent.PostId != postId)
            {
                return ForumResult<ForumCommentResponse>.Failure(ForumError.InvalidParent, "Odgovor ne pripada ovoj diskusiji.");
            }
        }

        var comment = await _repository.CreateCommentAsync(new Comment
        {
            PostId = postId,
            AutorId = authorId,
            ParentCommentId = request.ParentCommentId,
            Tekst = request.Tekst.Trim(),
            DatumKreiranja = DateTime.UtcNow
        }, cancellationToken);
        var users = await _repository.GetUsersAsync([authorId], cancellationToken);
        return ForumResult<ForumCommentResponse>.Success(
            MapComment(comment, 0, users, [], authorId));
    }

    public async Task<ForumResult<ForumVoteResponse>> VoteAsync(
        string commentId,
        string? userId,
        int value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ForumResult<ForumVoteResponse>.Failure(ForumError.Unauthorized, "Prijava je obavezna.");
        }

        var writeAccess = await CheckWriteAccessAsync(userId, cancellationToken);
        if (writeAccess is not null) return ForumResult<ForumVoteResponse>.Failure(ForumError.Forbidden, writeAccess);

        if (value is not (1 or -1))
        {
            return ForumResult<ForumVoteResponse>.Failure(ForumError.Validation, "Glas mora biti 1 ili -1.");
        }

        var comment = await _repository.GetCommentAsync(commentId, cancellationToken);

        if (comment is null || comment.Obrisan)
        {
            return ForumResult<ForumVoteResponse>.Failure(ForumError.NotFound, "Komentar nije pronadjen.");
        }

        if (comment.AutorId == userId)
        {
            return ForumResult<ForumVoteResponse>.Failure(ForumError.SelfVote, "Nije moguce glasati za sopstveni komentar.");
        }

        var now = DateTime.UtcNow;
        var existing = await _repository.GetVoteAsync(commentId, userId, cancellationToken);
        await _repository.UpsertVoteAsync(new CommentVote
        {
            Id = existing?.Id,
            CommentId = commentId,
            UserId = userId,
            Value = value,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        }, cancellationToken);

        return ForumResult<ForumVoteResponse>.Success(
            await BuildVoteResponseAsync(commentId, userId, cancellationToken));
    }

    public async Task<ForumResult<ForumVoteResponse>> RemoveVoteAsync(
        string commentId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return ForumResult<ForumVoteResponse>.Failure(ForumError.Unauthorized, "Prijava je obavezna.");
        }

        var writeAccess = await CheckWriteAccessAsync(userId, cancellationToken);
        if (writeAccess is not null) return ForumResult<ForumVoteResponse>.Failure(ForumError.Forbidden, writeAccess);

        if (await _repository.GetCommentAsync(commentId, cancellationToken) is null)
        {
            return ForumResult<ForumVoteResponse>.Failure(ForumError.NotFound, "Komentar nije pronadjen.");
        }

        await _repository.DeleteVoteAsync(commentId, userId, cancellationToken);
        return ForumResult<ForumVoteResponse>.Success(
            await BuildVoteResponseAsync(commentId, userId, cancellationToken));
    }

    private async Task<ForumVoteResponse> BuildVoteResponseAsync(
        string commentId,
        string userId,
        CancellationToken cancellationToken)
    {
        var votes = await _repository.GetVotesAsync([commentId], cancellationToken);
        return new ForumVoteResponse(
            commentId,
            votes.Count(vote => vote.Value == 1),
            votes.Count(vote => vote.Value == -1),
            votes.SingleOrDefault(vote => vote.UserId == userId)?.Value);
    }

    private static IReadOnlyList<Comment> SelectDisplayComments(IReadOnlyList<Comment> comments)
    {
        var parentIds = comments
            .Where(comment => comment.ParentCommentId is not null)
            .Select(comment => comment.ParentCommentId!)
            .ToHashSet(StringComparer.Ordinal);

        return comments
            .Where(comment => !comment.Obrisan || (comment.Id is not null && parentIds.Contains(comment.Id)))
            .OrderBy(comment => comment.DatumKreiranja)
            .ToList();
    }

    private static ForumDiscussionResponse MapDiscussion(
        Post post,
        IReadOnlyDictionary<string, User> users)
    {
        users.TryGetValue(post.AutorId, out var author);
        return new ForumDiscussionResponse(
            post.Id!,
            post.Naslov,
            post.Sadrzaj,
            post.AutorId,
            author?.Username ?? UnknownUser,
            author?.Uloga ?? "registrovani",
            post.DatumKreiranja,
            post.Istaknut);
    }

    private static ForumCommentResponse MapComment(
        Comment comment,
        int number,
        IReadOnlyDictionary<string, User> users,
        IReadOnlyList<CommentVote> votes,
        string? currentUserId)
    {
        users.TryGetValue(comment.AutorId, out var author);
        var commentVotes = votes.Where(vote => vote.CommentId == comment.Id).ToList();
        return new ForumCommentResponse(
            comment.Id!,
            comment.PostId,
            comment.ParentCommentId,
            comment.AutorId,
            author?.Username ?? UnknownUser,
            author?.Uloga ?? "registrovani",
            comment.Obrisan ? "Komentar je uklonjen" : comment.Tekst,
            comment.DatumKreiranja,
            comment.Obrisan,
            number,
            commentVotes.Count(vote => vote.Value == 1),
            commentVotes.Count(vote => vote.Value == -1),
            commentVotes.SingleOrDefault(vote => vote.UserId == currentUserId)?.Value);
    }

    private static string Username(IReadOnlyDictionary<string, User> users, string userId)
    {
        return users.TryGetValue(userId, out var user) ? user.Username : UnknownUser;
    }

    private async Task<string?> CheckWriteAccessAsync(string userId, CancellationToken cancellationToken)
    {
        if (_moderationService is null) return null;
        var access = await _moderationService.CheckForumWriteAsync(userId, cancellationToken);
        return access.Allowed ? null : access.Message ?? "Nalog nema dozvolu za ovu akciju.";
    }
}
