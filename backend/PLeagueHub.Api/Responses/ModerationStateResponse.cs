namespace PLeagueHub.Api.Responses;

public sealed record ModerationStateResponse(
    string Tip,
    string Razlog,
    DateTime Pocetak,
    DateTime? IsticeAt,
    string ModeratorId);
