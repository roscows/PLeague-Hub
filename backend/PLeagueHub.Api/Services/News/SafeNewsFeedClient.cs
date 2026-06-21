using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using PLeagueHub.Api.Configuration;

namespace PLeagueHub.Api.Services.News;

public sealed class SafeNewsFeedClient : INewsFeedClient
{
    private static readonly HashSet<string> XmlContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/rss+xml",
        "application/atom+xml",
        "application/xml",
        "text/xml"
    };

    private readonly HttpClient _httpClient;
    private readonly IPublicAddressResolver _resolver;
    private readonly NewsIngestionSettings _settings;

    public SafeNewsFeedClient(
        HttpClient httpClient,
        IPublicAddressResolver resolver,
        IOptions<NewsIngestionSettings> settings)
    {
        _httpClient = httpClient;
        _resolver = resolver;
        _settings = settings.Value;
    }

    public async Task<NewsFeedFetchResult> FetchAsync(
        string url,
        string? etag,
        DateTimeOffset? lastModified,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var currentUri))
            return Error(NewsFetchError.UnsafeAddress, "URL izvora nije validan.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_settings.RequestTimeout);

        try
        {
            for (var redirectCount = 0; ; redirectCount++)
            {
                if (!await IsSafeAsync(currentUri, timeout.Token))
                    return Error(NewsFetchError.UnsafeAddress, "URL izvora mora voditi na javnu HTTPS adresu.");

                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                request.Headers.UserAgent.ParseAdd("PLeagueHub/1.0 NewsAggregator");
                if (!string.IsNullOrWhiteSpace(etag)
                    && EntityTagHeaderValue.TryParse(etag, out var parsedEtag))
                    request.Headers.IfNoneMatch.Add(parsedEtag);
                if (lastModified.HasValue) request.Headers.IfModifiedSince = lastModified;

                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeout.Token);
                if (response.StatusCode == HttpStatusCode.NotModified)
                    return new(null, response.StatusCode, etag, lastModified, NewsFetchError.NotModified, null);

                if (IsRedirect(response.StatusCode))
                {
                    if (redirectCount >= _settings.MaxRedirects)
                        return Error(NewsFetchError.TooManyRedirects, "Izvor ima previse preusmerenja.");
                    if (response.Headers.Location is null)
                        return Error(NewsFetchError.HttpError, "Izvor je vratio preusmerenje bez lokacije.");
                    currentUri = response.Headers.Location.IsAbsoluteUri
                        ? response.Headers.Location
                        : new Uri(currentUri, response.Headers.Location);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    return new(null, response.StatusCode, null, null, NewsFetchError.HttpError,
                        $"Izvor je vratio HTTP status {(int)response.StatusCode}.");

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType is null || (!XmlContentTypes.Contains(mediaType) && !mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)))
                    return Error(NewsFetchError.InvalidContentType, "Izvor nije vratio RSS/Atom XML.");
                if (response.Content.Headers.ContentLength > _settings.MaxResponseBytes)
                    return Error(NewsFetchError.ResponseTooLarge, "RSS/Atom odgovor je prevelik.");

                var content = await ReadBoundedAsync(response.Content, timeout.Token);
                if (content is null)
                    return Error(NewsFetchError.ResponseTooLarge, "RSS/Atom odgovor je prevelik.");

                return new(
                    content,
                    response.StatusCode,
                    response.Headers.ETag?.ToString(),
                    response.Content.Headers.LastModified,
                    NewsFetchError.None,
                    null);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error(NewsFetchError.Timeout, "Provera izvora je istekla.");
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Net.Sockets.SocketException)
        {
            return Error(NewsFetchError.HttpError, "Povezivanje sa izvorom nije uspelo.");
        }
    }

    private async Task<bool> IsSafeAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.Port != 443)
            return false;

        if (IPAddress.TryParse(uri.DnsSafeHost, out var literalAddress))
            return PublicAddressValidator.IsPublic(literalAddress);

        var addresses = await _resolver.ResolveAsync(uri.DnsSafeHost, cancellationToken);
        return addresses.Length > 0 && addresses.All(PublicAddressValidator.IsPublic);
    }

    private async Task<MemoryStream?> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        var output = new MemoryStream();
        var buffer = new byte[16_384];
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (output.Length + read > _settings.MaxResponseBytes)
            {
                output.Dispose();
                return null;
            }
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        output.Position = 0;
        return output;
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Redirect or
        HttpStatusCode.RedirectMethod or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;

    private static NewsFeedFetchResult Error(NewsFetchError error, string message) =>
        new(null, null, null, null, error, message);
}
