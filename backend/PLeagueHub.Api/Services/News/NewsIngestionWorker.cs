using Microsoft.Extensions.Options;
using PLeagueHub.Api.Configuration;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Services.News;

public sealed class NewsIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NewsIngestionSettings _settings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NewsIngestionWorker> _logger;
    private readonly SemaphoreSlim _cycleLock = new(1, 1);

    public NewsIngestionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<NewsIngestionSettings> settings,
        TimeProvider timeProvider,
        ILogger<NewsIngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.WorkerEnabled)
        {
            _logger.LogInformation("Automatsko preuzimanje vesti je iskljuceno.");
            return;
        }

        using var timer = new PeriodicTimer(_settings.Interval, _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunCycleAsync(stoppingToken);
    }

    public async Task RunCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!_settings.WorkerEnabled || !await _cycleLock.WaitAsync(0, cancellationToken)) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<INewsRepository>();
            var ingestion = scope.ServiceProvider.GetRequiredService<INewsIngestionService>();
            var dueBefore = _timeProvider.GetUtcNow().UtcDateTime - _settings.Interval;
            var sources = await repository.GetDueSourcesAsync(dueBefore, cancellationToken);

            foreach (var source in sources)
            {
                try
                {
                    await ingestion.SyncSourceAsync(source.Id!, null, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Sinhronizacija izvora {SourceId} nije uspela.", source.Id);
                }
            }
        }
        finally
        {
            _cycleLock.Release();
        }
    }

    public override void Dispose()
    {
        _cycleLock.Dispose();
        base.Dispose();
    }
}
