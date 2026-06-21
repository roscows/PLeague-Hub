using System.Text.Json;

namespace PLeagueHub.Api.Services.Football;

public interface IFootballProvider
{
    Task<JsonDocument> SearchAsync(
        string term,
        CancellationToken cancellationToken = default);
}
