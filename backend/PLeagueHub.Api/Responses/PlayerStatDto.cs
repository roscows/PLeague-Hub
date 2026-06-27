namespace PLeagueHub.Api.Responses;

public sealed record PlayerStatDto(
    int Position,
    int ProviderId,
    string Ime,
    int TeamProviderId,
    string TeamNaziv,
    string TeamLogoUrl,
    int Golovi,
    int Asistencije,
    int Odigrano);
