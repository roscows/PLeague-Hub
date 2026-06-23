namespace PLeagueHub.Api.Services.Football;

public sealed class StandingsUnavailableException : Exception
{
    public StandingsUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
