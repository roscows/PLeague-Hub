using System.Text.Json;
using Microsoft.Extensions.Options;
using PLeagueHub.Api.Configuration;

namespace PLeagueHub.Api.Services.Football;

public sealed class FootApiClient : IFootballProvider
{
    private readonly HttpClient _httpClient;
    private readonly FootApiSettings _settings;

    public FootApiClient(HttpClient httpClient, IOptions<FootApiSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<JsonDocument> SearchAsync(
        string term,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException(
                "FootAPI key is missing. Configure FootApi:ApiKey in .NET User Secrets.");
        }

        var encodedTerm = Uri.EscapeDataString(term.Trim());
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/search/{encodedTerm}");
        request.Headers.Add("X-RapidAPI-Key", _settings.ApiKey);
        request.Headers.Add("X-RapidAPI-Host", _settings.Host);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
    }
}
