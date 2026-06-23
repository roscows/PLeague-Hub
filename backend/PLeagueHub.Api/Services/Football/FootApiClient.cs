using System.Text.Json;
using Microsoft.Extensions.Options;
using PLeagueHub.Api.Configuration;

namespace PLeagueHub.Api.Services.Football;

public sealed class FootApiClient : IFootballProvider
{
    private const int MaximumLogoBytes = 1_048_576;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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
        var encodedTerm = Uri.EscapeDataString(term.Trim());
        using var request = CreateRequest($"/api/search/{encodedTerm}");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(
            $"/api/tournament/{tournamentId}/season/{seasonId}/standings/total");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<FootApiStandingsResponse>(
            responseStream,
            JsonOptions,
            cancellationToken);

        return payload?.Standings?
            .SelectMany(group => group.Rows ?? [])
            .Where(row => row.Team is not null)
            .Select(row => new FootballTeamStanding(
                row.Team!.Id,
                row.Team.Name ?? string.Empty,
                row.Team.NameCode ?? string.Empty,
                row.Position,
                row.Matches,
                row.Wins,
                row.Draws,
                row.Losses,
                row.ScoresFor,
                row.ScoresAgainst,
                row.Points))
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyCollection<FootballSeason>> GetSeasonsAsync(
        int tournamentId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest($"/api/tournament/{tournamentId}/seasons");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<FootApiSeasonsResponse>(
            responseStream,
            JsonOptions,
            cancellationToken);

        return payload?.Seasons?
            .Select(season => new FootballSeason(
                season.Id,
                season.Name ?? string.Empty,
                season.Year ?? string.Empty))
            .ToArray() ?? [];
    }

    public async Task<FootballTeamLogo> GetTeamLogoAsync(
        int providerId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest($"/api/team/{providerId}/image");
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (string.IsNullOrWhiteSpace(contentType)
            || !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("FootAPI logo response is not an image.");
        }

        if (response.Content.Headers.ContentLength is > MaximumLogoBytes)
        {
            throw new InvalidDataException("FootAPI logo exceeds the 1 MB limit.");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var content = new MemoryStream();
        var buffer = new byte[81920];

        while (true)
        {
            var bytesRead = await responseStream.ReadAsync(buffer, cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            if (content.Length + bytesRead > MaximumLogoBytes)
            {
                throw new InvalidDataException("FootAPI logo exceeds the 1 MB limit.");
            }

            await content.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        if (content.Length == 0)
        {
            throw new InvalidDataException("FootAPI returned an empty team logo.");
        }

        return new FootballTeamLogo(content.ToArray(), contentType);
    }

    private HttpRequestMessage CreateRequest(string path)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException(
                "FootAPI key is missing. Configure FootApi:ApiKey in .NET User Secrets.");
        }

        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-RapidAPI-Key", _settings.ApiKey);
        request.Headers.Add("X-RapidAPI-Host", _settings.Host);
        return request;
    }

    private sealed record FootApiStandingsResponse(
        IReadOnlyCollection<FootApiStandingGroup>? Standings);

    private sealed record FootApiStandingGroup(
        IReadOnlyCollection<FootApiStandingRow>? Rows);

    private sealed record FootApiStandingRow(
        FootApiTeam? Team,
        int Position,
        int Matches,
        int Wins,
        int Draws,
        int Losses,
        int ScoresFor,
        int ScoresAgainst,
        int Points);

    private sealed record FootApiTeam(
        int Id,
        string? Name,
        string? NameCode);

    private sealed record FootApiSeasonsResponse(
        IReadOnlyCollection<FootApiSeason>? Seasons);

    private sealed record FootApiSeason(int Id, string? Name, string? Year);
}
