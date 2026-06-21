using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Services.News;

public sealed record NormalizedNewsEntry(
    string? ExternalId,
    string Title,
    string Excerpt,
    string? Author,
    string OriginalUrl,
    string? ImageUrl,
    DateTime PublishedAt);

public interface INewsFeedProvider
{
    Task<IReadOnlyList<NormalizedNewsEntry>> ParseAsync(
        Stream feed,
        NewsSource source,
        CancellationToken cancellationToken = default);
}
