using System.Net;

namespace PLeagueHub.Api.Services.News;

public enum NewsFetchError
{
    None,
    NotModified,
    UnsafeAddress,
    TooManyRedirects,
    ResponseTooLarge,
    InvalidContentType,
    Timeout,
    HttpError
}

public sealed record NewsFeedFetchResult(
    Stream? Content,
    HttpStatusCode? StatusCode,
    string? Etag,
    DateTimeOffset? LastModified,
    NewsFetchError Error,
    string? Message);

public interface INewsFeedClient
{
    Task<NewsFeedFetchResult> FetchAsync(
        string url,
        string? etag,
        DateTimeOffset? lastModified,
        CancellationToken cancellationToken = default);
}
