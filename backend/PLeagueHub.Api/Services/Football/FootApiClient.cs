using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PLeagueHub.Api.Configuration;

namespace PLeagueHub.Api.Services.Football;

public sealed class FootApiClient : IFootballProvider
{
    private const int MaximumLogoBytes = 1_048_576;
    private const int MaxPagesPerBucket = 60;
    private const int MatchFetchDelayMs = 1200;
    private const int RateLimitBackoffMs = 2500;
    private const int MaxRateLimitRetries = 5;
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

    public async Task<IReadOnlyCollection<FootballEvent>> GetSeasonEventsAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        var events = new Dictionary<int, FootballEvent>();

        foreach (var bucket in new[] { "last", "next" })
        {
            var page = 0;
            var rateLimitRetries = 0;

            while (page <= MaxPagesPerBucket)
            {
                await Task.Delay(MatchFetchDelayMs, cancellationToken);

                using var request = CreateRequest(
                    $"/api/tournament/{tournamentId}/season/{seasonId}/matches/{bucket}/{page}");
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
                {
                    break;
                }

                if ((int)response.StatusCode == 429)
                {
                    if (++rateLimitRetries > MaxRateLimitRetries)
                    {
                        break;
                    }

                    await Task.Delay(RateLimitBackoffMs, cancellationToken);
                    continue;
                }

                rateLimitRetries = 0;
                response.EnsureSuccessStatusCode();

                await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<FootApiEventsResponse>(
                    responseStream,
                    JsonOptions,
                    cancellationToken);

                var rows = payload?.Events ?? [];

                foreach (var row in rows.Where(item => item.HomeTeam is not null && item.AwayTeam is not null))
                {
                    events[row.Id] = new FootballEvent(
                        row.Id,
                        row.RoundInfo?.Round ?? 0,
                        row.HomeTeam!.Id,
                        row.HomeTeam.Name ?? string.Empty,
                        row.HomeTeam.NameCode ?? string.Empty,
                        row.AwayTeam!.Id,
                        row.AwayTeam.Name ?? string.Empty,
                        row.AwayTeam.NameCode ?? string.Empty,
                        row.HomeScore?.Current,
                        row.AwayScore?.Current,
                        row.Status?.Type ?? string.Empty,
                        row.StartTimestamp);
                }

                if (rows.Count == 0 || payload?.HasNextPage == false)
                {
                    break;
                }

                page++;
            }
        }

        return events.Values.ToArray();
    }

    public Task<FootballTeamLogo> GetTeamLogoAsync(
        int providerId,
        CancellationToken cancellationToken = default)
        => FetchImageAsync($"/api/team/{providerId}/image", cancellationToken);

    public Task<FootballTeamLogo> GetPlayerImageAsync(
        int playerId,
        CancellationToken cancellationToken = default)
        => FetchImageAsync($"/api/player/{playerId}/image", cancellationToken);

    private async Task<FootballTeamLogo> FetchImageAsync(
        string path,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(path);
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

    public async Task<IReadOnlyCollection<FootballStatItem>> GetMatchStatisticsAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetMatchResourceAsync<FootApiStatisticsResponse>(
            eventId, "statistics", cancellationToken);

        var allPeriod = payload?.Statistics?.FirstOrDefault(period =>
            string.Equals(period.Period, "ALL", StringComparison.OrdinalIgnoreCase));

        return allPeriod?.Groups?
            .SelectMany(group => group.StatisticsItems ?? [])
            .Select(item => new FootballStatItem(
                item.Name ?? string.Empty,
                item.Home ?? string.Empty,
                item.Away ?? string.Empty))
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyCollection<FootballIncident>> GetMatchIncidentsAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetMatchResourceAsync<FootApiIncidentsResponse>(
            eventId, "incidents", cancellationToken);

        return payload?.Incidents?
            .Where(incident => !string.IsNullOrWhiteSpace(incident.IncidentType))
            .Select(incident => new FootballIncident(
                incident.IncidentType!,
                incident.Time,
                incident.IsHome ?? false,
                incident.Player?.Name ?? string.Empty,
                incident.PlayerIn?.Name ?? string.Empty,
                incident.PlayerOut?.Name ?? string.Empty,
                incident.Text ?? string.Empty))
            .ToArray() ?? [];
    }

    public async Task<FootballLineups?> GetMatchLineupsAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetMatchResourceAsync<FootApiLineupsResponse>(
            eventId, "lineups", cancellationToken);

        if (payload is null)
        {
            return null;
        }

        return new FootballLineups(
            payload.Confirmed ?? false,
            MapLineupTeam(payload.Home),
            MapLineupTeam(payload.Away));
    }

    private static FootballLineupTeam? MapLineupTeam(FootApiLineupTeam? team)
    {
        if (team is null)
        {
            return null;
        }

        var players = (team.Players ?? [])
            .Select(entry => new FootballLineupPlayer(
                entry.Player?.Name ?? string.Empty,
                int.TryParse(entry.JerseyNumber, out var number) ? number : 0,
                entry.Substitute ?? false,
                entry.Position ?? string.Empty))
            .ToArray();

        return new FootballLineupTeam(team.Formation ?? string.Empty, players);
    }

    public async Task<IReadOnlyCollection<FootballPlayerStat>> GetBestPlayersAsync(
        int tournamentId,
        int seasonId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest($"/api/tournament/{tournamentId}/season/{seasonId}/best-players");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<FootApiBestPlayersResponse>(
            responseStream, JsonOptions, cancellationToken);

        var players = new Dictionary<int, PlayerAccumulator>();
        Accumulate(payload?.TopPlayers?.Goals, players, isGoals: true);
        Accumulate(payload?.TopPlayers?.Assists, players, isGoals: false);

        return players.Values
            .Select(player => new FootballPlayerStat(
                player.Id, player.Name, player.TeamId, player.TeamName,
                player.Goals, player.Assists, player.Appearances))
            .ToArray();
    }

    public async Task<FootballPlayerProfile?> GetPlayerProfileAsync(
        int playerId,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest($"/api/player/{playerId}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<FootApiPlayerResponse>(
            responseStream, JsonOptions, cancellationToken);

        var player = payload?.Player;

        if (player is null)
        {
            return null;
        }

        DateTime? dateOfBirth = player.DateOfBirthTimestamp is long timestamp
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime
            : null;

        return new FootballPlayerProfile(
            player.Id,
            player.Name ?? string.Empty,
            player.Position ?? string.Empty,
            player.Height ?? 0,
            dateOfBirth,
            player.Country?.Name ?? string.Empty,
            player.Team?.Id ?? 0,
            player.Team?.Name ?? string.Empty);
    }

    private static void Accumulate(
        IReadOnlyCollection<FootApiPlayerEntry>? entries,
        Dictionary<int, PlayerAccumulator> players,
        bool isGoals)
    {
        foreach (var entry in entries ?? [])
        {
            if (entry.Player is null)
            {
                continue;
            }

            if (!players.TryGetValue(entry.Player.Id, out var accumulator))
            {
                accumulator = new PlayerAccumulator { Id = entry.Player.Id };
                players[entry.Player.Id] = accumulator;
            }

            if (string.IsNullOrEmpty(accumulator.Name))
            {
                accumulator.Name = entry.Player.Name ?? string.Empty;
            }

            if (entry.Team is not null && string.IsNullOrEmpty(accumulator.TeamName))
            {
                accumulator.TeamId = entry.Team.Id;
                accumulator.TeamName = entry.Team.Name ?? string.Empty;
            }

            if (entry.Statistics is not null)
            {
                if (isGoals && entry.Statistics.Goals is int goals)
                {
                    accumulator.Goals = goals;
                }

                if (!isGoals && entry.Statistics.Assists is int assists)
                {
                    accumulator.Assists = assists;
                }

                if (entry.Statistics.Appearances is int appearances && accumulator.Appearances == 0)
                {
                    accumulator.Appearances = appearances;
                }
            }
        }
    }

    private sealed class PlayerAccumulator
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Appearances { get; set; }
    }

    private async Task<TPayload?> GetMatchResourceAsync<TPayload>(
        int eventId,
        string resource,
        CancellationToken cancellationToken)
        where TPayload : class
    {
        using var request = CreateRequest($"/api/match/{eventId}/{resource}");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<TPayload>(responseStream, JsonOptions, cancellationToken);
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

    private sealed record FootApiEventsResponse(
        bool? HasNextPage,
        IReadOnlyCollection<FootApiEvent>? Events);

    private sealed record FootApiEvent(
        int Id,
        FootApiRoundInfo? RoundInfo,
        FootApiEventTeam? HomeTeam,
        FootApiEventTeam? AwayTeam,
        FootApiScore? HomeScore,
        FootApiScore? AwayScore,
        FootApiStatus? Status,
        long StartTimestamp);

    private sealed record FootApiRoundInfo(int Round);

    private sealed record FootApiEventTeam(int Id, string? Name, string? NameCode);

    private sealed record FootApiScore(int? Current);

    private sealed record FootApiStatus(string? Type);

    private sealed record FootApiStatisticsResponse(IReadOnlyCollection<FootApiStatPeriod>? Statistics);
    private sealed record FootApiStatPeriod(string? Period, IReadOnlyCollection<FootApiStatGroup>? Groups);
    private sealed record FootApiStatGroup(string? GroupName, IReadOnlyCollection<FootApiStatEntry>? StatisticsItems);
    private sealed record FootApiStatEntry(string? Name, string? Home, string? Away);

    private sealed record FootApiIncidentsResponse(IReadOnlyCollection<FootApiIncident>? Incidents);
    private sealed record FootApiIncident(
        string? IncidentType,
        int Time,
        bool? IsHome,
        FootApiNamed? Player,
        FootApiNamed? PlayerIn,
        FootApiNamed? PlayerOut,
        string? Text);
    private sealed record FootApiNamed(string? Name);

    private sealed record FootApiLineupsResponse(bool? Confirmed, FootApiLineupTeam? Home, FootApiLineupTeam? Away);
    private sealed record FootApiLineupTeam(string? Formation, IReadOnlyCollection<FootApiLineupEntry>? Players);
    private sealed record FootApiLineupEntry(FootApiNamed? Player, string? JerseyNumber, bool? Substitute, string? Position);

    private sealed record FootApiBestPlayersResponse(FootApiTopPlayers? TopPlayers);
    private sealed record FootApiTopPlayers(
        IReadOnlyCollection<FootApiPlayerEntry>? Goals,
        IReadOnlyCollection<FootApiPlayerEntry>? Assists);
    private sealed record FootApiPlayerEntry(FootApiPlayerRef? Player, FootApiTeamRef? Team, FootApiPlayerStatistics? Statistics);
    private sealed record FootApiPlayerRef(int Id, string? Name);
    private sealed record FootApiTeamRef(int Id, string? Name);
    private sealed record FootApiPlayerStatistics(int? Goals, int? Assists, int? Appearances);

    private sealed record FootApiPlayerResponse(FootApiPlayerDetail? Player);
    private sealed record FootApiPlayerDetail(
        int Id,
        string? Name,
        string? Position,
        int? Height,
        long? DateOfBirthTimestamp,
        FootApiCountry? Country,
        FootApiTeamRef? Team);
    private sealed record FootApiCountry(string? Name);
}
