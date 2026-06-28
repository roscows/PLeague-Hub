namespace PLeagueHub.Api.Responses;

public sealed record ModerationActivityDto(
    string Id,
    string Akcija,
    string? TipMere,
    string ModeratorUsername,
    string KorisnikUsername,
    string? Razlog,
    DateTime Datum);
