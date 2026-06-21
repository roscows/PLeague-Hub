using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Tests;

public sealed class ForumServiceTests
{
    private const string PostId = "665000000000000000000801";
    private const string AuthorId = "665000000000000000000501";
    private const string VoterId = "665000000000000000000503";
    private const string RootId = "665000000000000000000811";
    private const string ChildId = "665000000000000000000812";

    [Fact]
    public async Task GetTopicsAsync_EnrichesRepliesAuthorAndLatestActivity()
    {
        var repository = CreateRepository();
        var service = new ForumService(repository);

        var page = await service.GetTopicsAsync(new ForumListRequest(), CancellationToken.None);

        var topic = Assert.Single(page.Items);
        Assert.Equal("admin", topic.AutorUsername);
        Assert.Equal(2, topic.BrojOdgovora);
        Assert.Equal("fan", topic.PoslednjiAutorUsername);
        Assert.Equal(new DateTime(2026, 6, 21, 11, 10, 0, DateTimeKind.Utc), topic.PoslednjaAktivnost);
    }

    [Fact]
    public async Task GetCommentsAsync_PreservesDeletedParentAndOmitsDeletedLeaf()
    {
        var repository = CreateRepository();
        repository.Comments.Add(new Comment
        {
            Id = "665000000000000000000813",
            PostId = PostId,
            AutorId = AuthorId,
            Tekst = "Obrisani list",
            DatumKreiranja = new DateTime(2026, 6, 21, 11, 20, 0, DateTimeKind.Utc),
            Obrisan = true
        });
        repository.Comments.Single(comment => comment.Id == RootId).Obrisan = true;
        var service = new ForumService(repository);

        var comments = await service.GetCommentsAsync(PostId, VoterId, CancellationToken.None);

        Assert.NotNull(comments);
        Assert.Equal(2, comments.Count);
        Assert.Equal("Komentar je uklonjen", comments[0].Tekst);
        Assert.True(comments[0].Obrisan);
        Assert.Equal(RootId, comments[1].ParentCommentId);
    }

    [Fact]
    public async Task CreateCommentAsync_RejectsParentFromAnotherDiscussion()
    {
        var repository = CreateRepository();
        repository.Comments.Single(comment => comment.Id == RootId).PostId = "665000000000000000000899";
        var service = new ForumService(repository);

        var result = await service.CreateCommentAsync(
            PostId,
            new CreateCommentRequest { Tekst = "Odgovor", ParentCommentId = RootId },
            VoterId,
            CancellationToken.None);

        Assert.Equal(ForumError.InvalidParent, result.Error);
    }

    [Fact]
    public async Task VoteAsync_RejectsVoteOnOwnComment()
    {
        var repository = CreateRepository();
        var service = new ForumService(repository);

        var result = await service.VoteAsync(RootId, AuthorId, 1, CancellationToken.None);

        Assert.Equal(ForumError.SelfVote, result.Error);
        Assert.Empty(repository.Votes);
    }

    [Fact]
    public async Task VoteAsync_ChangesAndRemovesSingleUserVote()
    {
        var repository = CreateRepository();
        var service = new ForumService(repository);

        var liked = await service.VoteAsync(RootId, VoterId, 1, CancellationToken.None);
        var disliked = await service.VoteAsync(RootId, VoterId, -1, CancellationToken.None);
        var removed = await service.RemoveVoteAsync(RootId, VoterId, CancellationToken.None);

        Assert.Equal(1, liked.Value!.Lajkovi);
        Assert.Equal(-1, disliked.Value!.TrenutniGlas);
        Assert.Equal(1, disliked.Value.Dislajkovi);
        Assert.Equal(0, removed.Value!.Lajkovi + removed.Value.Dislajkovi);
        Assert.Empty(repository.Votes);
    }

    private static FakeForumRepository CreateRepository()
    {
        var repository = new FakeForumRepository();
        repository.Posts.Add(new Post
        {
            Id = PostId,
            AutorId = AuthorId,
            Naslov = "Pravila foruma",
            Sadrzaj = "Razgovarajte uz postovanje.",
            Tip = "diskusija",
            DatumKreiranja = new DateTime(2026, 6, 21, 10, 0, 0, DateTimeKind.Utc),
            PoslednjaAktivnost = new DateTime(2026, 6, 21, 11, 10, 0, DateTimeKind.Utc),
            Istaknut = true
        });
        repository.Users.AddRange(
        [
            new User { Id = AuthorId, Username = "admin", Uloga = "administrator" },
            new User { Id = VoterId, Username = "fan", Uloga = "registrovani" }
        ]);
        repository.Comments.AddRange(
        [
            new Comment
            {
                Id = RootId,
                PostId = PostId,
                AutorId = AuthorId,
                Tekst = "Prvi komentar",
                DatumKreiranja = new DateTime(2026, 6, 21, 11, 0, 0, DateTimeKind.Utc)
            },
            new Comment
            {
                Id = ChildId,
                PostId = PostId,
                AutorId = VoterId,
                ParentCommentId = RootId,
                Tekst = "Odgovor",
                DatumKreiranja = new DateTime(2026, 6, 21, 11, 10, 0, DateTimeKind.Utc)
            }
        ]);
        return repository;
    }

    private sealed class FakeForumRepository : IForumRepository
    {
        public List<Post> Posts { get; } = [];
        public List<Comment> Comments { get; } = [];
        public List<User> Users { get; } = [];
        public List<CommentVote> Votes { get; } = [];

        public Task<ForumTopicPage> GetTopicsAsync(ForumQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(new ForumTopicPage(Posts, Posts.Count));

        public Task<Post?> GetVisibleDiscussionAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Posts.SingleOrDefault(post => post.Id == id && !post.Obrisan));

        public Task<Comment?> GetCommentAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(Comments.SingleOrDefault(comment => comment.Id == id));

        public Task<IReadOnlyList<Comment>> GetCommentsAsync(string postId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Comment>>(Comments.Where(comment => comment.PostId == postId).OrderBy(comment => comment.DatumKreiranja).ToList());

        public Task<IReadOnlyList<Comment>> GetCommentsForPostsAsync(IReadOnlyCollection<string> postIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Comment>>(Comments.Where(comment => postIds.Contains(comment.PostId)).ToList());

        public Task<IReadOnlyDictionary<string, User>> GetUsersAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, User>>(Users.Where(user => user.Id is not null && userIds.Contains(user.Id)).ToDictionary(user => user.Id!));

        public Task<IReadOnlyList<CommentVote>> GetVotesAsync(IReadOnlyCollection<string> commentIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CommentVote>>(Votes.Where(vote => commentIds.Contains(vote.CommentId)).ToList());

        public Task<Post> CreateDiscussionAsync(Post post, CancellationToken cancellationToken = default)
        {
            post.Id ??= Guid.NewGuid().ToString("N");
            Posts.Add(post);
            return Task.FromResult(post);
        }

        public Task<Comment> CreateCommentAsync(Comment comment, CancellationToken cancellationToken = default)
        {
            comment.Id ??= Guid.NewGuid().ToString("N");
            Comments.Add(comment);
            return Task.FromResult(comment);
        }

        public Task<CommentVote?> GetVoteAsync(string commentId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Votes.SingleOrDefault(vote => vote.CommentId == commentId && vote.UserId == userId));

        public Task UpsertVoteAsync(CommentVote vote, CancellationToken cancellationToken = default)
        {
            Votes.RemoveAll(existing => existing.CommentId == vote.CommentId && existing.UserId == vote.UserId);
            Votes.Add(vote);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteVoteAsync(string commentId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Votes.RemoveAll(vote => vote.CommentId == commentId && vote.UserId == userId) > 0);
    }
}
