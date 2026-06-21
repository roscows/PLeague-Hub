using System.Net;
using System.ServiceModel.Syndication;
using System.Xml;
using AngleSharp.Html.Parser;
using Ganss.Xss;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Services.News;

public sealed class SyndicationNewsFeedProvider : INewsFeedProvider
{
    private const int MaxExcerptLength = 500;

    public Task<IReadOnlyList<NormalizedNewsEntry>> ParseAsync(
        Stream feed,
        NewsSource source,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var reader = XmlReader.Create(feed, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                CloseInput = false
            });
            var syndicationFeed = SyndicationFeed.Load(reader)
                ?? throw new InvalidDataException("RSS/Atom izvor je prazan.");
            var entries = syndicationFeed.Items
                .Select(MapEntry)
                .Where(entry => entry is not null)
                .Cast<NormalizedNewsEntry>()
                .ToList();
            return Task.FromResult<IReadOnlyList<NormalizedNewsEntry>>(entries);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"Izvor '{source.Naziv}' nema validan RSS/Atom format.", exception);
        }
    }

    private static NormalizedNewsEntry? MapEntry(SyndicationItem item)
    {
        var title = NormalizeText(item.Title?.Text);
        var link = item.Links.FirstOrDefault(candidate =>
                candidate.Uri is not null
                && (string.IsNullOrEmpty(candidate.RelationshipType)
                    || candidate.RelationshipType.Equals("alternate", StringComparison.OrdinalIgnoreCase)))
            ?.Uri;
        var published = item.PublishDate != DateTimeOffset.MinValue
            ? item.PublishDate
            : item.LastUpdatedTime;

        if (string.IsNullOrWhiteSpace(title)
            || link is null
            || !link.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || published == DateTimeOffset.MinValue)
        {
            return null;
        }

        var content = item.Summary?.Text
            ?? (item.Content as TextSyndicationContent)?.Text
            ?? string.Empty;
        var author = item.Authors.Select(candidate => candidate.Name ?? candidate.Email)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var image = item.Links.FirstOrDefault(candidate =>
            candidate.Uri is not null
            && candidate.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)?.Uri;

        return new NormalizedNewsEntry(
            string.IsNullOrWhiteSpace(item.Id) ? null : item.Id.Trim(),
            title,
            SanitizeExcerpt(content),
            NormalizeText(author),
            link.AbsoluteUri,
            image?.Scheme == Uri.UriSchemeHttps ? image.AbsoluteUri : null,
            published.UtcDateTime);
    }

    private static string SanitizeExcerpt(string html)
    {
        var sanitizer = new HtmlSanitizer();
        var safeHtml = sanitizer.Sanitize(html);
        var decoded = WebUtility.HtmlDecode(new HtmlParser().ParseDocument(safeHtml).Body?.TextContent ?? string.Empty);
        var normalized = NormalizeText(decoded);
        return normalized.Length <= MaxExcerptLength
            ? normalized
            : normalized[..MaxExcerptLength].TrimEnd();
    }

    private static string NormalizeText(string? value) => string.Join(
        ' ',
        (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
