namespace PLeagueHub.Api.Services.Football;

public sealed class ProfileUnavailableException : Exception
{
    public ProfileUnavailableException(string message)
        : base(message)
    {
    }

    public ProfileUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
