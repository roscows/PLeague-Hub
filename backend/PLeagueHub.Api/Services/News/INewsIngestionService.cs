namespace PLeagueHub.Api.Services.News;

public enum IngestionOutcome
{
    Created,
    Duplicate,
    PromotedToOfficial,
    SkippedIrrelevant,
    Failed
}

public sealed record NewsIngestionEntryResult(
    string Title,
    IngestionOutcome Outcome,
    string? PostId = null,
    string? Message = null);

public sealed record NewsSourceSyncResponse(
    string SourceId,
    bool Success,
    bool NotModified,
    int Created,
    int Duplicates,
    int Promoted,
    int Skipped,
    string? Error,
    IReadOnlyList<NewsIngestionEntryResult> Entries);

public interface INewsIngestionService
{
    Task<NewsSourceSyncResponse> SyncSourceAsync(
        string sourceId,
        string? actorId,
        CancellationToken cancellationToken = default);
}
