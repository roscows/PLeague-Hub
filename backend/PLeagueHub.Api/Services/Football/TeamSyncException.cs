namespace PLeagueHub.Api.Services.Football;

public sealed class TeamSyncException(string message, Exception? innerException = null)
    : Exception(message, innerException);
