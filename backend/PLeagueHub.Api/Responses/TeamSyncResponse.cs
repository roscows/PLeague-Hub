namespace PLeagueHub.Api.Responses;

public sealed record TeamSyncResponse(
    int TournamentId,
    int SeasonId,
    int Created,
    int Updated,
    int Skipped);
