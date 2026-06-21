using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Repositories;

public sealed record NewsQuery(
    string? Category,
    string? Reliability,
    string? SourceId,
    DateTime? BeforePublishedAt,
    string? BeforeId,
    int Limit);

public interface INewsRepository
{
    Task<IReadOnlyList<Post>> GetTimelineAsync(NewsQuery query, CancellationToken cancellationToken = default);

    Task<Post?> GetVisibleAsync(string id, CancellationToken cancellationToken = default);

    Task<Post?> FindDuplicateAsync(
        string? externalId,
        string originalUrl,
        string fingerprint,
        CancellationToken cancellationToken = default);

    Task<Post> CreateAsync(Post post, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(string id, Post post, CancellationToken cancellationToken = default);

    Task<bool> PromoteRumorAsync(
        string id,
        Post officialPost,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NewsSource>> GetDueSourcesAsync(
        DateTime dueBefore,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NewsSource>> GetSourcesAsync(CancellationToken cancellationToken = default);

    Task<NewsSource?> GetSourceAsync(string id, CancellationToken cancellationToken = default);

    Task<NewsSource> CreateSourceAsync(NewsSource source, CancellationToken cancellationToken = default);

    Task<bool> UpdateSourceAsync(
        string id,
        NewsSource source,
        CancellationToken cancellationToken = default);

    Task MarkSourceSuccessAsync(
        string id,
        DateTime checkedAt,
        string? etag,
        DateTimeOffset? lastModified,
        CancellationToken cancellationToken = default);

    Task MarkSourceFailureAsync(
        string id,
        DateTime checkedAt,
        string reason,
        int pauseAfter,
        CancellationToken cancellationToken = default);

    Task RecordAuditAsync(
        EditorialAuditEvent auditEvent,
        CancellationToken cancellationToken = default);
}
