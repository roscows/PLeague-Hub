namespace PLeagueHub.Api.Services.Football;

public sealed class PlayerStatsSyncException : Exception
{
    public PlayerStatsSyncException(string message)
        : base(message)
    {
    }

    public PlayerStatsSyncException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
