using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IClubProfileService
{
    Task<ClubProfileDto?> GetAsync(int providerId, CancellationToken cancellationToken);
}

// Club details + roster are fetched from FootApi on the first view and persisted
// to MongoDB (lazy, like MatchDetailService). Standings position, recent form and
// last matches are computed from the stored Match collection (no live call).
public sealed class ClubProfileService : IClubProfileService
{
    private const string FinishedStatus = "zavrsena";
    private const int RecentCount = 5;

    private readonly IRepository<ClubProfileDocument> _profiles;
    private readonly IRepository<Team> _teams;
    private readonly IRepository<Match> _matches;
    private readonly IStandingsService _standings;
    private readonly IFootballProvider _provider;
    private readonly IProviderRequestPacer _pacer;

    public ClubProfileService(
        IRepository<ClubProfileDocument> profiles,
        IRepository<Team> teams,
        IRepository<Match> matches,
        IStandingsService standings,
        IFootballProvider provider,
        IProviderRequestPacer pacer)
    {
        _profiles = profiles;
        _teams = teams;
        _matches = matches;
        _standings = standings;
        _provider = provider;
        _pacer = pacer;
    }

    public async Task<ClubProfileDto?> GetAsync(int providerId, CancellationToken cancellationToken)
    {
        var team = await _teams.FindOneAsync(item => item.ProviderId == providerId, cancellationToken);

        if (team is null)
        {
            return null;
        }

        var document = await _profiles.FindOneAsync(profile => profile.ProviderId == providerId, cancellationToken)
            ?? await FetchAndPersistAsync(providerId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var allTeams = (await _teams.GetAllAsync(cancellationToken))
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First());

        var (season, position) = await ResolveSeasonAndPositionAsync(providerId, cancellationToken);
        var recent = await BuildRecentAsync(team.Id, season, allTeams, cancellationToken);

        return new ClubProfileDto(
            providerId,
            team.Naziv,
            team.LogoUrl,
            document.Stadion,
            document.Osnovan,
            document.Trener,
            document.Drzava,
            position,
            season,
            recent.Select(match => match.Ishod).ToArray(),
            recent,
            document.Roster
                .Select(player => new ClubRosterDto(player.ProviderId, player.Ime, player.Pozicija, player.Broj, player.Drzava))
                .ToArray());
    }

    private async Task<ClubProfileDocument?> FetchAndPersistAsync(int providerId, CancellationToken cancellationToken)
    {
        FootballTeamDetails? details;
        IReadOnlyCollection<FootballRosterPlayer> roster;

        try
        {
            await _pacer.WaitAsync(cancellationToken);
            details = await _provider.GetTeamDetailsAsync(providerId, cancellationToken);
            await _pacer.WaitAsync(cancellationToken);
            roster = await _provider.GetTeamPlayersAsync(providerId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new ProfileUnavailableException("FootAPI club profile could not be loaded.", exception);
        }

        if (details is null)
        {
            return null;
        }

        var document = new ClubProfileDocument
        {
            ProviderId = providerId,
            Stadion = details.Stadium,
            Osnovan = details.Founded,
            Trener = details.Manager,
            Drzava = details.Country,
            Roster = roster
                .Select(player => new ClubRosterEntry
                {
                    ProviderId = player.ProviderId,
                    Ime = player.Name,
                    Pozicija = player.Position,
                    Broj = player.Number,
                    Drzava = player.Country
                })
                .ToList(),
            FetchedAt = DateTime.UtcNow
        };

        return await _profiles.CreateAsync(document, cancellationToken);
    }

    private async Task<(string Season, int Position)> ResolveSeasonAndPositionAsync(
        int providerId,
        CancellationToken cancellationToken)
    {
        var season = (await _matches.GetAllAsync(cancellationToken))
            .Where(match => IsFinished(match))
            .Select(match => match.Sezona)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .OrderByDescending(label => label, StringComparer.Ordinal)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(season))
        {
            return (string.Empty, 0);
        }

        var standings = await _standings.GetStandingsAsync(season, cancellationToken);
        var row = standings.FirstOrDefault(entry => entry.ProviderId == providerId);

        return (season, row?.Position ?? 0);
    }

    private async Task<IReadOnlyCollection<ClubMatchDto>> BuildRecentAsync(
        string teamId,
        string season,
        IReadOnlyDictionary<string, Team> teams,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(season))
        {
            return [];
        }

        var matches = (await _matches.GetAllAsync(cancellationToken))
            .Where(match => IsFinished(match)
                && string.Equals(match.Sezona, season, StringComparison.Ordinal)
                && (match.DomacinId == teamId || match.GostId == teamId))
            .OrderByDescending(match => match.Datum)
            .Take(RecentCount)
            .ToArray();

        return matches.Select(match => BuildMatchDto(match, teamId, teams)).ToArray();
    }

    private static ClubMatchDto BuildMatchDto(Match match, string teamId, IReadOnlyDictionary<string, Team> teams)
    {
        var home = match.DomacinId == teamId;
        var opponentId = home ? match.GostId : match.DomacinId;
        teams.TryGetValue(opponentId, out var opponent);

        var golMi = home ? match.GolDomacin : match.GolGost;
        var golProtivnik = home ? match.GolGost : match.GolDomacin;
        var ishod = golMi > golProtivnik ? "W" : golMi < golProtivnik ? "L" : "D";

        return new ClubMatchDto(
            match.Id ?? string.Empty,
            match.Sezona,
            match.Datum.ToString("o"),
            opponent?.Naziv ?? "Nepoznat tim",
            opponent?.LogoUrl ?? string.Empty,
            home,
            golMi,
            golProtivnik,
            ishod);
    }

    private static bool IsFinished(Match match)
        => string.Equals(match.Status, FinishedStatus, StringComparison.Ordinal)
            && match.GolDomacin.HasValue
            && match.GolGost.HasValue;
}
