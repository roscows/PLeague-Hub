namespace PLeagueHub.Api.Responses;

public sealed record NewsTimelineResponse(
    IReadOnlyList<NewsTimelineItemResponse> Items,
    string? NextCursor);

public sealed record NewsTimelineItemResponse(
    string Id,
    string Naslov,
    string Sazetak,
    string Kategorija,
    string Pouzdanost,
    string? SourceId,
    string? IzvorNaziv,
    string? AutorId,
    string? AutorUsername,
    string? OriginalUrl,
    string? ImageUrl,
    string? XEmbedUrl,
    DateTime PublishedAt,
    DateTime? FetchedAt,
    bool UvozAutomatski,
    int BrojKomentara);

public sealed record NewsDetailResponse(
    string Id,
    string Naslov,
    string Sadrzaj,
    string Kategorija,
    string Pouzdanost,
    string? SourceId,
    string? IzvorNaziv,
    string? AutorId,
    string? AutorUsername,
    string? ExternalAuthor,
    string? OriginalUrl,
    string? ImageUrl,
    string? XEmbedUrl,
    DateTime PublishedAt,
    DateTime? FetchedAt,
    DateTime? UpdatedAt,
    bool UvozAutomatski,
    int BrojKomentara);

public sealed record NewsSourceResponse(
    string Id,
    string Naziv,
    string FeedUrl,
    string SiteUrl,
    string Tip,
    string PodrazumevanaKategorija,
    string PodrazumevanaPouzdanost,
    IReadOnlyList<string> UkljuceniPojmovi,
    IReadOnlyList<string> IskljuceniPojmovi,
    bool Aktivan,
    string? PauziranRazlog,
    int UzastopneGreske,
    DateTime? PoslednjaProveraAt,
    DateTime? PoslednjiUspehAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
