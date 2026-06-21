namespace PLeagueHub.Api.Responses;

public sealed record TeamLogoSyncResponse(
    int Downloaded,
    int Updated,
    int Skipped,
    int Failed,
    IReadOnlyCollection<int> FailedProviderIds);
