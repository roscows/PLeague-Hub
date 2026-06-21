using MongoDB.Driver;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Data;

public sealed class MongoIndexInitializer
{
    private readonly MongoContext _context;

    public MongoIndexInitializer(MongoContext context)
    {
        _context = context;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await CreateTeamIndexesAsync(cancellationToken);
        await CreatePlayerIndexesAsync(cancellationToken);
        await CreateMatchIndexesAsync(cancellationToken);
        await CreateStatisticIndexesAsync(cancellationToken);
        await CreateUserIndexesAsync(cancellationToken);
        await CreatePostIndexesAsync(cancellationToken);
        await CreateCommentIndexesAsync(cancellationToken);
        await CreateCommentVoteIndexesAsync(cancellationToken);
        await CreateModerationActionIndexesAsync(cancellationToken);
        await CreateNewsSourceIndexesAsync(cancellationToken);
        await CreateEditorialAuditEventIndexesAsync(cancellationToken);
    }

    private async Task CreateTeamIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Team>(
                Builders<Team>.IndexKeys.Ascending(team => team.Pozicija),
                new CreateIndexOptions { Name = "idx_teams_pozicija" }),
            new CreateIndexModel<Team>(
                Builders<Team>.IndexKeys.Ascending(team => team.Skracenica),
                new CreateIndexOptions { Name = "idx_teams_skracenica" }),
            new CreateIndexModel<Team>(
                Builders<Team>.IndexKeys.Ascending(team => team.ProviderId),
                new CreateIndexOptions
                {
                    Name = "idx_teams_provider_id",
                    Unique = true,
                    Sparse = true
                })
        };

        await _context.Teams.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreatePlayerIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Player>(
                Builders<Player>.IndexKeys.Ascending(player => player.TeamId),
                new CreateIndexOptions { Name = "idx_players_teamId" }),
            new CreateIndexModel<Player>(
                Builders<Player>.IndexKeys.Descending(player => player.Golovi),
                new CreateIndexOptions { Name = "idx_players_golovi" }),
            new CreateIndexModel<Player>(
                Builders<Player>.IndexKeys.Descending(player => player.Asistencije),
                new CreateIndexOptions { Name = "idx_players_asistencije" })
        };

        await _context.Players.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreateMatchIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Match>(
                Builders<Match>.IndexKeys
                    .Ascending(match => match.Sezona)
                    .Ascending(match => match.Kolo),
                new CreateIndexOptions { Name = "idx_matches_sezona_kolo" }),
            new CreateIndexModel<Match>(
                Builders<Match>.IndexKeys.Ascending(match => match.Datum),
                new CreateIndexOptions { Name = "idx_matches_datum" }),
            new CreateIndexModel<Match>(
                Builders<Match>.IndexKeys.Ascending(match => match.Status),
                new CreateIndexOptions { Name = "idx_matches_status" })
        };

        await _context.Matches.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreateStatisticIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Statistic>(
                Builders<Statistic>.IndexKeys.Ascending(statistic => statistic.MatchId),
                new CreateIndexOptions { Name = "idx_statistics_matchId" }),
            new CreateIndexModel<Statistic>(
                Builders<Statistic>.IndexKeys.Ascending(statistic => statistic.PlayerId),
                new CreateIndexOptions { Name = "idx_statistics_playerId" })
        };

        await _context.Statistics.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreateUserIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(user => user.Email),
                new CreateIndexOptions { Name = "idx_users_email", Unique = true }),
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(user => user.Username),
                new CreateIndexOptions { Name = "idx_users_username", Unique = true })
        };

        await _context.Users.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreatePostIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Post>(
                Builders<Post>.IndexKeys
                    .Ascending(post => post.Tip)
                    .Ascending(post => post.Obrisan)
                    .Descending(post => post.DatumKreiranja),
                new CreateIndexOptions { Name = "idx_posts_tip_obrisan_datumKreiranja" }),
            new CreateIndexModel<Post>(
                Builders<Post>.IndexKeys.Ascending(post => post.AutorId),
                new CreateIndexOptions { Name = "idx_posts_autorId" }),
            new CreateIndexModel<Post>(
                Builders<Post>.IndexKeys
                    .Ascending(post => post.Tip)
                    .Descending(post => post.PublishedAt)
                    .Descending(post => post.Id),
                new CreateIndexOptions { Name = "idx_posts_tip_publishedAt_id" }),
            new CreateIndexModel<Post>(
                Builders<Post>.IndexKeys.Ascending(post => post.OriginalUrl),
                new CreateIndexOptions { Name = "uq_posts_originalUrl", Unique = true, Sparse = true }),
            new CreateIndexModel<Post>(
                Builders<Post>.IndexKeys.Ascending(post => post.ExternalId),
                new CreateIndexOptions { Name = "uq_posts_externalId", Unique = true, Sparse = true }),
            new CreateIndexModel<Post>(
                Builders<Post>.IndexKeys.Ascending(post => post.Fingerprint),
                new CreateIndexOptions { Name = "idx_posts_fingerprint", Sparse = true })
        };

        await _context.Posts.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreateCommentIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<Comment>(
                Builders<Comment>.IndexKeys
                    .Ascending(comment => comment.PostId)
                    .Ascending(comment => comment.ParentCommentId)
                    .Ascending(comment => comment.DatumKreiranja),
                new CreateIndexOptions { Name = "idx_comments_postId_parentId_datumKreiranja" }),
            new CreateIndexModel<Comment>(
                Builders<Comment>.IndexKeys.Ascending(comment => comment.AutorId),
                new CreateIndexOptions { Name = "idx_comments_autorId" }),
            new CreateIndexModel<Comment>(
                Builders<Comment>.IndexKeys
                    .Ascending(comment => comment.PostId)
                    .Descending(comment => comment.Istaknut)
                    .Descending(comment => comment.IstaknutAt),
                new CreateIndexOptions { Name = "idx_comments_postId_istaknut_istaknutAt" })
        };

        await _context.Comments.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreateCommentVoteIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<CommentVote>(
                Builders<CommentVote>.IndexKeys
                    .Ascending(vote => vote.CommentId)
                    .Ascending(vote => vote.UserId),
                new CreateIndexOptions
                {
                    Name = "uq_commentVotes_commentId_userId",
                    Unique = true
                }),
            new CreateIndexModel<CommentVote>(
                Builders<CommentVote>.IndexKeys
                    .Ascending(vote => vote.CommentId)
                    .Ascending(vote => vote.Value),
                new CreateIndexOptions { Name = "idx_commentVotes_commentId_value" })
        };

        await _context.CommentVotes.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreateModerationActionIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ModerationAction>(
                Builders<ModerationAction>.IndexKeys
                    .Ascending(action => action.KorisnikId)
                    .Descending(action => action.Datum),
                new CreateIndexOptions { Name = "idx_moderationActions_korisnikId_datum" })
        };

        await _context.ModerationActions.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreateNewsSourceIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<NewsSource>(
                Builders<NewsSource>.IndexKeys
                    .Ascending(source => source.Aktivan)
                    .Ascending(source => source.PoslednjaProveraAt),
                new CreateIndexOptions { Name = "idx_newsSources_aktivan_poslednjaProveraAt" }),
            new CreateIndexModel<NewsSource>(
                Builders<NewsSource>.IndexKeys.Ascending(source => source.FeedUrl),
                new CreateIndexOptions { Name = "uq_newsSources_feedUrl", Unique = true })
        };

        await _context.NewsSources.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    private async Task CreateEditorialAuditEventIndexesAsync(CancellationToken cancellationToken)
    {
        var indexes = new[]
        {
            new CreateIndexModel<EditorialAuditEvent>(
                Builders<EditorialAuditEvent>.IndexKeys
                    .Ascending(audit => audit.TargetType)
                    .Ascending(audit => audit.TargetId)
                    .Descending(audit => audit.Datum),
                new CreateIndexOptions { Name = "idx_editorialAudit_target_datum" })
        };

        await _context.EditorialAuditEvents.Indexes.CreateManyAsync(indexes, cancellationToken);
    }
}
