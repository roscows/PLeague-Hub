namespace PLeagueHub.Api.Services.Football;

public sealed class MatchSyncException : Exception
{
    public MatchSyncException(string message)
        : base(message)
    {
    }

    public MatchSyncException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
