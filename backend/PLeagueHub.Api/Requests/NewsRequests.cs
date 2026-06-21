namespace PLeagueHub.Api.Requests;

public sealed record NewsTimelineRequest
{
    public string? Kategorija { get; init; }
    public string? Pouzdanost { get; init; }
    public string? SourceId { get; init; }
    public DateTime? PreDatuma { get; init; }
    public string? Cursor { get; init; }
    public int Limit { get; init; } = 20;
}

public sealed record CreateXNewsRequest
{
    public string Naslov { get; init; } = string.Empty;
    public string XUrl { get; init; } = string.Empty;
    public string Kategorija { get; init; } = "premier_league";
    public string Pouzdanost { get; init; } = "pouzdan_izvor";
}

public sealed record UpdateNewsRequest
{
    public string? Naslov { get; init; }
    public string? Sadrzaj { get; init; }
    public string? Kategorija { get; init; }
    public string? Pouzdanost { get; init; }
    public string? ImageUrl { get; init; }
    public string? OriginalUrl { get; init; }
}

public record CreateNewsSourceRequest
{
    public string Naziv { get; init; } = string.Empty;
    public string FeedUrl { get; init; } = string.Empty;
    public string SiteUrl { get; init; } = string.Empty;
    public string PodrazumevanaKategorija { get; init; } = "premier_league";
    public string PodrazumevanaPouzdanost { get; init; } = "pouzdan_izvor";
    public List<string> UkljuceniPojmovi { get; init; } = [];
    public List<string> IskljuceniPojmovi { get; init; } = [];
    public bool Aktivan { get; init; }
}

public sealed record UpdateNewsSourceRequest : CreateNewsSourceRequest;

public sealed record PauseNewsSourceRequest
{
    public string Razlog { get; init; } = string.Empty;
}
