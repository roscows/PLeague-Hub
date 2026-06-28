namespace PLeagueHub.Api.Responses;

public sealed record PanelUserDto(
    string Id,
    string Username,
    string Email,
    string Uloga,
    bool Aktivan,
    string? AktivnaMera);
