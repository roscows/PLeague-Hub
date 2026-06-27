namespace PLeagueHub.Api.Responses;

public sealed record PlayerProfileDto(
    int ProviderId,
    string Ime,
    string Pozicija,
    string Drzava,
    int Visina,
    int? Godine,
    string KlubNaziv,
    int KlubProviderId,
    string FotoUrl,
    IReadOnlyCollection<PlayerSeasonLineDto> Sezone);

public sealed record PlayerSeasonLineDto(
    string Sezona,
    string TeamNaziv,
    int TeamProviderId,
    int Golovi,
    int Asistencije,
    int Odigrano);
