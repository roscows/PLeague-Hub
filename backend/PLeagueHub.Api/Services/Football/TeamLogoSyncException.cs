namespace PLeagueHub.Api.Services.Football;

public sealed class TeamLogoSyncException(string message, Exception? innerException = null)
    : Exception(message, innerException);
