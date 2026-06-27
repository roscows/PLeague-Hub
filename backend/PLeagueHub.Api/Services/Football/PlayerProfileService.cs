using System.Text.Json;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services.Football;

public interface IPlayerProfileService
{
    Task<PlayerProfileDto?> GetAsync(int providerId, CancellationToken cancellationToken);
}

// Player profile (bio + photo) is fetched from FootApi on the first view and
// persisted to MongoDB, so later views (and demos) read from the database with
// no live FootApi call. Per-season stats are joined from PlayerSeasonStats.
public sealed class PlayerProfileService : IPlayerProfileService
{
    private readonly IRepository<PlayerProfileDocument> _profiles;
    private readonly IRepository<PlayerSeasonStatDocument> _stats;
    private readonly IFootballProvider _provider;
    private readonly IPlayerPhotoCache _photos;
    private readonly IProviderRequestPacer _pacer;

    public PlayerProfileService(
        IRepository<PlayerProfileDocument> profiles,
        IRepository<PlayerSeasonStatDocument> stats,
        IFootballProvider provider,
        IPlayerPhotoCache photos,
        IProviderRequestPacer pacer)
    {
        _profiles = profiles;
        _stats = stats;
        _provider = provider;
        _photos = photos;
        _pacer = pacer;
    }

    public async Task<PlayerProfileDto?> GetAsync(int providerId, CancellationToken cancellationToken)
    {
        var document = await _profiles.FindOneAsync(profile => profile.ProviderId == providerId, cancellationToken)
            ?? await FetchAndPersistAsync(providerId, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var seasons = (await _stats.GetAllAsync(cancellationToken))
            .Where(stat => stat.ProviderId == providerId)
            .OrderByDescending(stat => stat.Sezona, StringComparer.Ordinal)
            .Select(stat => new PlayerSeasonLineDto(
                stat.Sezona,
                stat.TeamNaziv,
                stat.TeamProviderId,
                stat.Golovi,
                stat.Asistencije,
                stat.Odigrano))
            .ToArray();

        int? godine = document.DatumRodjenja is DateTime dateOfBirth ? CalculateAge(dateOfBirth) : null;

        return new PlayerProfileDto(
            document.ProviderId,
            document.Ime,
            document.Pozicija,
            document.Drzava,
            document.Visina,
            godine,
            document.KlubNaziv,
            document.KlubProviderId,
            document.FotoUrl,
            seasons);
    }

    private async Task<PlayerProfileDocument?> FetchAndPersistAsync(int providerId, CancellationToken cancellationToken)
    {
        FootballPlayerProfile? profile;

        try
        {
            await _pacer.WaitAsync(cancellationToken);
            profile = await _provider.GetPlayerProfileAsync(providerId, cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            throw new ProfileUnavailableException("FootAPI player profile could not be loaded.", exception);
        }

        if (profile is null)
        {
            return null;
        }

        var fotoUrl = await TrySavePhotoAsync(providerId, cancellationToken);

        var document = new PlayerProfileDocument
        {
            ProviderId = profile.ProviderId,
            Ime = profile.Name,
            Pozicija = profile.Position,
            Drzava = profile.Country,
            Visina = profile.Height,
            DatumRodjenja = profile.DateOfBirth,
            KlubNaziv = profile.TeamName,
            KlubProviderId = profile.TeamId,
            FotoUrl = fotoUrl,
            FetchedAt = DateTime.UtcNow
        };

        return await _profiles.CreateAsync(document, cancellationToken);
    }

    // The photo is optional decoration; a failure here must not block the bio.
    private async Task<string> TrySavePhotoAsync(int providerId, CancellationToken cancellationToken)
    {
        try
        {
            await _pacer.WaitAsync(cancellationToken);
            var photo = await _provider.GetPlayerImageAsync(providerId, cancellationToken);
            await _photos.SaveAsync(providerId, photo, cancellationToken);
            return _photos.GetPublicUrl(providerId);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidDataException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            return string.Empty;
        }
    }

    private static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.UtcNow;
        var age = today.Year - dateOfBirth.Year;

        if (dateOfBirth.Date > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}
