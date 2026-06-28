namespace PLeagueHub.Api.Responses;

public sealed record StaffNoticeDto(
    string Id,
    string Tekst,
    string AutorUsername,
    bool Pinovano,
    DateTime DatumKreiranja);
