namespace PLeagueHub.Api.Services.Football;

public sealed class MatchDetailUnavailableException : Exception
{
    public MatchDetailUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
