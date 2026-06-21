namespace PLeagueHub.Api.Requests;

public sealed class CreateModerationActionRequest
{
    public string Tip { get; set; } = string.Empty;

    public string Trajanje { get; set; } = string.Empty;

    public string Razlog { get; set; } = string.Empty;
}
