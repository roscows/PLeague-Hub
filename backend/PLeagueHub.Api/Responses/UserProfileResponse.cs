namespace PLeagueHub.Api.Responses;

public sealed record UserProfileResponse
{
    public string UserId { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Uloga { get; init; } = string.Empty;

    public bool Aktivan { get; init; }

    public DateTime DatumReg { get; init; }

    public IReadOnlyCollection<string> FavoritniTimovi { get; init; } = [];
}
