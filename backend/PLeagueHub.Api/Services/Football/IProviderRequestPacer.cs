namespace PLeagueHub.Api.Services.Football;

public interface IProviderRequestPacer
{
    Task WaitAsync(CancellationToken cancellationToken = default);
}
