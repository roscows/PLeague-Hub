using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.News;

namespace PLeagueHub.Api.Services;

public enum NewsError
{
    None,
    Validation,
    NotFound,
    Conflict
}

public sealed record NewsResult<T>(T? Value, NewsError Error, string? Message)
    where T : class
{
    public static NewsResult<T> Success(T value) => new(value, NewsError.None, null);
    public static NewsResult<T> Failure(NewsError error, string message) => new(null, error, message);
}

public interface INewsService
{
    Task<NewsResult<NewsTimelineResponse>> GetTimelineAsync(NewsTimelineRequest request, CancellationToken cancellationToken = default);
    Task<NewsDetailResponse?> GetDetailAsync(string id, CancellationToken cancellationToken = default);
    Task<NewsResult<NewsDetailResponse>> CreateAsync(CreateNewsRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<NewsResult<NewsDetailResponse>> CreateXAsync(CreateXNewsRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<NewsResult<NewsDetailResponse>> UpdateAsync(string id, UpdateNewsRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<NewsResult<NewsDetailResponse>> DeleteAsync(string id, string actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NewsSourceResponse>> GetSourcesAsync(CancellationToken cancellationToken = default);
    Task<NewsResult<NewsSourceResponse>> CreateSourceAsync(CreateNewsSourceRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<NewsResult<NewsSourceResponse>> UpdateSourceAsync(string id, UpdateNewsSourceRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<NewsResult<NewsSourceResponse>> DeactivateSourceAsync(string id, string actorId, CancellationToken cancellationToken = default);
    Task<NewsResult<NewsSourceResponse>> PauseSourceAsync(string id, PauseNewsSourceRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<NewsResult<NewsSourceResponse>> ResumeSourceAsync(string id, string actorId, CancellationToken cancellationToken = default);
    Task<NewsResult<NewsSourceSyncResponse>> SyncSourceAsync(string id, string actorId, CancellationToken cancellationToken = default);
}
