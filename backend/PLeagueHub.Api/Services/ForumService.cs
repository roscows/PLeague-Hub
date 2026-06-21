using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public sealed class ForumService : IForumService
{
    private const string UnknownUser = "Nepoznat korisnik";
    private readonly IForumRepository _repository;
    private readonly ICommentService _commentService;
    private readonly IModerationService? _moderationService;

    public ForumService(IForumRepository repository)
        : this(repository, new CommentService(repository), null)
    {
    }

    public ForumService(IForumRepository repository, IModerationService? moderationService)
        : this(repository, new CommentService(repository, moderationService), moderationService)
    {
    }

    public ForumService(
        IForumRepository repository,
        ICommentService commentService,
        IModerationService? moderationService)
    {
        _repository = repository;
        _commentService = commentService;
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
            .Where(userId => userId is not null)
            .Cast<string>()
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
                ?? (post.PoslednjaAktivnost > DateTime.MinValue
                    ? post.PoslednjaAktivnost
                    : post.DatumKreiranja);

            return new ForumTopicResponse(
                post.Id!, post.Naslov, post.AutorId!, Username(users, post.AutorId!),
                Role(users, post.AutorId!), postComments.Count, post.DatumKreiranja,
                latestActivity,
                latestComment is null
                    ? Username(users, post.AutorId!)
                    : Username(users, latestComment.AutorId),
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
        if (post is null || post.AutorId is null) return null;

        var users = await _repository.GetUsersAsync([post.AutorId], cancellationToken);
        return MapDiscussion(post, users);
    }

    public Task<IReadOnlyList<ForumCommentResponse>?> GetCommentsAsync(
        string postId,
        string? currentUserId,
        CancellationToken cancellationToken = default) =>
        _commentService.GetCommentsAsync(postId, currentUserId, cancellationToken);

    public async Task<ForumResult<ForumDiscussionResponse>> CreateDiscussionAsync(
        CreateForumPostRequest request,
        string? authorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorId))
            return ForumResult<ForumDiscussionResponse>.Failure(
                ForumError.Unauthorized, "Prijava je obavezna.");

        var writeAccess = await CheckWriteAccessAsync(authorId, cancellationToken);
        if (writeAccess is not null)
            return ForumResult<ForumDiscussionResponse>.Failure(ForumError.Forbidden, writeAccess);
        if (string.IsNullOrWhiteSpace(request.Naslov) || string.IsNullOrWhiteSpace(request.Sadrzaj))
            return ForumResult<ForumDiscussionResponse>.Failure(
                ForumError.Validation, "Naslov i sadrzaj su obavezni.");

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

    public Task<ForumResult<ForumCommentResponse>> CreateCommentAsync(
        string postId,
        CreateCommentRequest request,
        string? authorId,
        CancellationToken cancellationToken = default) =>
        _commentService.CreateAsync(postId, request, authorId, cancellationToken);

    public Task<ForumResult<ForumVoteResponse>> VoteAsync(
        string commentId,
        string? userId,
        int value,
        CancellationToken cancellationToken = default) =>
        _commentService.VoteAsync(commentId, userId, value, cancellationToken);

    public Task<ForumResult<ForumVoteResponse>> RemoveVoteAsync(
        string commentId,
        string? userId,
        CancellationToken cancellationToken = default) =>
        _commentService.RemoveVoteAsync(commentId, userId, cancellationToken);

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
        users.TryGetValue(post.AutorId!, out var author);
        return new ForumDiscussionResponse(
            post.Id!, post.Naslov, post.Sadrzaj, post.AutorId!,
            author?.Username ?? UnknownUser, author?.Uloga ?? "registrovani",
            post.DatumKreiranja, post.Istaknut);
    }

    private static string Username(IReadOnlyDictionary<string, User> users, string userId) =>
        users.TryGetValue(userId, out var user) ? user.Username : UnknownUser;

    private static string Role(IReadOnlyDictionary<string, User> users, string userId) =>
        users.TryGetValue(userId, out var user) ? user.Uloga : "registrovani";

    private async Task<string?> CheckWriteAccessAsync(string userId, CancellationToken cancellationToken)
    {
        if (_moderationService is null) return null;
        var access = await _moderationService.CheckForumWriteAsync(userId, cancellationToken);
        return access.Allowed ? null : access.Message ?? "Nalog nema dozvolu za ovu akciju.";
    }
}
