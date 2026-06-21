namespace PLeagueHub.Api.Services.Football;

public sealed class ProviderRequestPacer : IProviderRequestPacer, IDisposable
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(275);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset? _lastRequestAt;

    public ProviderRequestPacer()
        : this(TimeProvider.System)
    {
    }

    internal ProviderRequestPacer(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var now = _timeProvider.GetUtcNow();

            if (_lastRequestAt is DateTimeOffset lastRequestAt)
            {
                var remaining = MinimumInterval - (now - lastRequestAt);

                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, _timeProvider, cancellationToken);
                }
            }

            _lastRequestAt = _timeProvider.GetUtcNow();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
