namespace PLeagueHub.Api.Responses;

public sealed record MatchSyncResponse(
    int Total,
    int Created,
    int Updated,
    int TeamsCreated);
