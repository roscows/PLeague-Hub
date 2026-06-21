using MongoDB.Bson;
using MongoDB.Driver;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Services.News;

public sealed class NewsIngestionService : INewsIngestionService
{
    private const int PauseAfterErrors = 3;
    private readonly INewsRepository _repository;
    private readonly INewsFeedClient _feedClient;
    private readonly INewsFeedProvider _feedProvider;
    private readonly NewsRelevanceFilter _relevanceFilter;
    private readonly TimeProvider _timeProvider;

    public NewsIngestionService(
        INewsRepository repository,
        INewsFeedClient feedClient,
        INewsFeedProvider feedProvider,
        NewsRelevanceFilter relevanceFilter,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _feedClient = feedClient;
        _feedProvider = feedProvider;
        _relevanceFilter = relevanceFilter;
        _timeProvider = timeProvider;
    }

    public async Task<NewsSourceSyncResponse> SyncSourceAsync(
        string sourceId,
        string? actorId,
        CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetSourceAsync(sourceId, cancellationToken);
        if (source is null)
            return Failed(sourceId, "Izvor vesti nije pronadjen.");

        var checkedAt = _timeProvider.GetUtcNow().UtcDateTime;
        NewsFeedFetchResult fetch;
        try
        {
            fetch = await _feedClient.FetchAsync(
                source.FeedUrl,
                source.Etag,
                source.LastModified,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await RecordFailureAsync(source, checkedAt, "Povezivanje sa izvorom nije uspelo.", cancellationToken);
        }

        if (fetch.Error == NewsFetchError.NotModified)
        {
            await _repository.MarkSourceSuccessAsync(
                source.Id!, checkedAt, fetch.Etag ?? source.Etag,
                fetch.LastModified ?? source.LastModified, cancellationToken);
            return new(source.Id!, true, true, 0, 0, 0, 0, null, []);
        }

        if (fetch.Error != NewsFetchError.None || fetch.Content is null)
            return await RecordFailureAsync(
                source, checkedAt, fetch.Message ?? "Preuzimanje izvora nije uspelo.", cancellationToken);

        IReadOnlyList<NormalizedNewsEntry> entries;
        await using (fetch.Content)
        {
            try
            {
                entries = await _feedProvider.ParseAsync(fetch.Content, source, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return await RecordFailureAsync(
                    source, checkedAt, "RSS/Atom sadrzaj nije moguce procitati.", cancellationToken);
            }
        }

        var results = new List<NewsIngestionEntryResult>();
        foreach (var entry in entries.OrderBy(item => item.PublishedAt))
        {
            var searchableText = $"{entry.Title} {entry.Excerpt}";
            if (!_relevanceFilter.IsRelevant(searchableText, source))
            {
                results.Add(new(entry.Title, IngestionOutcome.SkippedIrrelevant));
                continue;
            }

            results.Add(await ProcessEntryAsync(source, entry, checkedAt, actorId, cancellationToken));
        }

        await _repository.MarkSourceSuccessAsync(
            source.Id!, checkedAt, fetch.Etag, fetch.LastModified, cancellationToken);
        return BuildSuccess(source.Id!, results);
    }

    private async Task<NewsIngestionEntryResult> ProcessEntryAsync(
        NewsSource source,
        NormalizedNewsEntry entry,
        DateTime fetchedAt,
        string? actorId,
        CancellationToken cancellationToken)
    {
        var originalUrl = NewsFingerprint.NormalizeUrl(entry.OriginalUrl);
        var fingerprint = NewsFingerprint.Create(originalUrl, entry.Title);
        var post = CreatePost(source, entry, originalUrl, fingerprint, fetchedAt);
        var duplicate = await _repository.FindDuplicateAsync(
            entry.ExternalId, originalUrl, fingerprint, cancellationToken);

        if (duplicate is not null)
            return await HandleDuplicateAsync(duplicate, post, source, actorId, cancellationToken);

        try
        {
            var created = await _repository.CreateAsync(post, cancellationToken);
            return new(entry.Title, IngestionOutcome.Created, created.Id);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            duplicate = await _repository.FindDuplicateAsync(
                entry.ExternalId, originalUrl, fingerprint, cancellationToken);
            return duplicate is null
                ? new(entry.Title, IngestionOutcome.Failed, Message: "Vest nije sacuvana zbog konflikta podataka.")
                : await HandleDuplicateAsync(duplicate, post, source, actorId, cancellationToken);
        }
    }

    private async Task<NewsIngestionEntryResult> HandleDuplicateAsync(
        Post duplicate,
        Post incoming,
        NewsSource source,
        string? actorId,
        CancellationToken cancellationToken)
    {
        if (duplicate.Pouzdanost == "glasina" && source.PodrazumevanaPouzdanost == "zvanicno"
            && await _repository.PromoteRumorAsync(duplicate.Id!, incoming, cancellationToken))
        {
            if (ObjectId.TryParse(actorId, out _))
            {
                await _repository.RecordAuditAsync(new EditorialAuditEvent
                {
                    ActorId = actorId!,
                    TargetType = "post",
                    TargetId = duplicate.Id!,
                    Akcija = "promocija_u_zvanicno",
                    Datum = _timeProvider.GetUtcNow().UtcDateTime
                }, cancellationToken);
            }
            return new(incoming.Naslov, IngestionOutcome.PromotedToOfficial, duplicate.Id);
        }

        return new(incoming.Naslov, IngestionOutcome.Duplicate, duplicate.Id);
    }

    private static Post CreatePost(
        NewsSource source,
        NormalizedNewsEntry entry,
        string originalUrl,
        string fingerprint,
        DateTime fetchedAt) => new()
    {
        AutorId = null,
        Naslov = entry.Title,
        Sadrzaj = entry.Excerpt,
        Tip = "vest",
        DatumKreiranja = fetchedAt,
        PoslednjaAktivnost = entry.PublishedAt,
        SourceId = source.Id,
        OriginalUrl = originalUrl,
        ExternalId = entry.ExternalId,
        ExternalAuthor = entry.Author,
        Kategorija = source.PodrazumevanaKategorija,
        Pouzdanost = source.PodrazumevanaPouzdanost,
        Fingerprint = fingerprint,
        PublishedAt = DateTime.SpecifyKind(entry.PublishedAt, DateTimeKind.Utc),
        FetchedAt = fetchedAt,
        ImageUrl = entry.ImageUrl,
        UvozAutomatski = true,
        UpdatedAt = fetchedAt
    };

    private async Task<NewsSourceSyncResponse> RecordFailureAsync(
        NewsSource source,
        DateTime checkedAt,
        string reason,
        CancellationToken cancellationToken)
    {
        await _repository.MarkSourceFailureAsync(
            source.Id!, checkedAt, reason, PauseAfterErrors, cancellationToken);
        return Failed(source.Id!, reason);
    }

    private static NewsSourceSyncResponse BuildSuccess(
        string sourceId,
        IReadOnlyList<NewsIngestionEntryResult> entries) => new(
        sourceId,
        true,
        false,
        entries.Count(entry => entry.Outcome == IngestionOutcome.Created),
        entries.Count(entry => entry.Outcome == IngestionOutcome.Duplicate),
        entries.Count(entry => entry.Outcome == IngestionOutcome.PromotedToOfficial),
        entries.Count(entry => entry.Outcome == IngestionOutcome.SkippedIrrelevant),
        null,
        entries);

    private static NewsSourceSyncResponse Failed(string sourceId, string error) =>
        new(sourceId, false, false, 0, 0, 0, 0, error, []);
}
