using MongoDB.Bson;
using MongoDB.Driver;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.News;

namespace PLeagueHub.Api.Services;

public sealed class NewsService : INewsService
{
    private static readonly HashSet<string> Categories =
        new(["premier_league", "transferi", "fpl", "klubovi"], StringComparer.Ordinal);
    private static readonly HashSet<string> Reliability =
        new(["zvanicno", "pouzdan_izvor", "glasina", "fpl_analiza"], StringComparer.Ordinal);

    private readonly INewsRepository _repository;
    private readonly IForumRepository _forumRepository;
    private readonly INewsIngestionService _ingestionService;
    private readonly TimeProvider _timeProvider;

    public NewsService(
        INewsRepository repository,
        IForumRepository forumRepository,
        INewsIngestionService ingestionService,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _forumRepository = forumRepository;
        _ingestionService = ingestionService;
        _timeProvider = timeProvider;
    }

    public async Task<NewsResult<NewsTimelineResponse>> GetTimelineAsync(
        NewsTimelineRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Limit is < 1 or > 50)
            return NewsResult<NewsTimelineResponse>.Failure(NewsError.Validation, "Limit mora biti izmedju 1 i 50.");
        if (!ValidOptional(request.Kategorija, Categories))
            return NewsResult<NewsTimelineResponse>.Failure(NewsError.Validation, "Kategorija vesti nije validna.");
        if (!ValidOptional(request.Pouzdanost, Reliability))
            return NewsResult<NewsTimelineResponse>.Failure(NewsError.Validation, "Oznaka pouzdanosti nije validna.");
        if (request.SourceId is not null && !ObjectId.TryParse(request.SourceId, out _))
            return NewsResult<NewsTimelineResponse>.Failure(NewsError.Validation, "ID izvora nije validan.");

        DateTime? beforePublishedAt = request.PreDatuma?.ToUniversalTime();
        string? beforeId = beforePublishedAt.HasValue ? "ffffffffffffffffffffffff" : null;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            try
            {
                (beforePublishedAt, beforeId) = NewsCursorCodec.Decode(request.Cursor);
                if (!ObjectId.TryParse(beforeId, out _)) throw new FormatException("Kursor nije validan.");
            }
            catch (FormatException exception)
            {
                return NewsResult<NewsTimelineResponse>.Failure(NewsError.Validation, exception.Message);
            }
        }

        var posts = await _repository.GetTimelineAsync(new NewsQuery(
            request.Kategorija, request.Pouzdanost, request.SourceId,
            beforePublishedAt, beforeId, request.Limit + 1), cancellationToken);
        var hasMore = posts.Count > request.Limit;
        var page = posts.Take(request.Limit).ToList();
        var context = await LoadMappingContextAsync(page, cancellationToken);
        var items = page.Select(post => MapTimelineItem(post, context)).ToList();
        var last = hasMore ? page.LastOrDefault() : null;
        var nextCursor = last is null
            ? null
            : NewsCursorCodec.Encode(PublishedAt(last), last.Id!);
        return NewsResult<NewsTimelineResponse>.Success(new(items, nextCursor));
    }

    public async Task<NewsDetailResponse?> GetDetailAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var post = await _repository.GetVisibleAsync(id, cancellationToken);
        if (post is null) return null;
        var context = await LoadMappingContextAsync([post], cancellationToken);
        return MapDetail(post, context);
    }

    public async Task<NewsResult<NewsDetailResponse>> CreateAsync(
        CreateNewsRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateArticle(request.Naslov, request.Sadrzaj, request.Kategorija,
            request.Pouzdanost, request.ImageUrl, request.OriginalUrl);
        if (error is not null) return NewsResult<NewsDetailResponse>.Failure(NewsError.Validation, error);

        var now = UtcNow();
        var originalUrl = NormalizeOptionalUrl(request.OriginalUrl);
        var post = new Post
        {
            AutorId = actorId,
            Naslov = request.Naslov.Trim(),
            Sadrzaj = request.Sadrzaj.Trim(),
            Tip = "vest",
            DatumKreiranja = now,
            PoslednjaAktivnost = now,
            Kategorija = request.Kategorija,
            Pouzdanost = request.Pouzdanost,
            OriginalUrl = originalUrl,
            ImageUrl = PreserveOptionalHttpsUrl(request.ImageUrl),
            Fingerprint = originalUrl is null ? null : NewsFingerprint.Create(originalUrl, request.Naslov),
            PublishedAt = now,
            FetchedAt = now,
            UvozAutomatski = false,
            UpdatedAt = now
        };
        return await CreatePostAsync(post, actorId, "kreiranje_vesti", cancellationToken);
    }

    public async Task<NewsResult<NewsDetailResponse>> CreateXAsync(
        CreateXNewsRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Naslov))
            return NewsResult<NewsDetailResponse>.Failure(NewsError.Validation, "Naslov je obavezan.");
        if (!ValidRequired(request.Kategorija, Categories) || !ValidRequired(request.Pouzdanost, Reliability))
            return NewsResult<NewsDetailResponse>.Failure(NewsError.Validation, "Kategorija ili pouzdanost nije validna.");
        if (!TryNormalizeXUrl(request.XUrl, out var xUrl))
            return NewsResult<NewsDetailResponse>.Failure(
                NewsError.Validation, "X URL mora imati oblik https://x.com/korisnik/status/broj.");

        var now = UtcNow();
        var post = new Post
        {
            AutorId = actorId,
            Naslov = request.Naslov.Trim(),
            Sadrzaj = string.Empty,
            Tip = "vest",
            DatumKreiranja = now,
            PoslednjaAktivnost = now,
            Kategorija = request.Kategorija,
            Pouzdanost = request.Pouzdanost,
            OriginalUrl = xUrl,
            XEmbedUrl = xUrl,
            Fingerprint = NewsFingerprint.Create(xUrl, request.Naslov),
            PublishedAt = now,
            FetchedAt = now,
            UvozAutomatski = false,
            UpdatedAt = now
        };
        return await CreatePostAsync(post, actorId, "kreiranje_x_vesti", cancellationToken);
    }

    public async Task<NewsResult<NewsDetailResponse>> UpdateAsync(
        string id,
        UpdateNewsRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var post = await _repository.GetVisibleAsync(id, cancellationToken);
        if (post is null)
            return NewsResult<NewsDetailResponse>.Failure(NewsError.NotFound, "Vest nije pronadjena.");
        if (request.Naslov is not null && string.IsNullOrWhiteSpace(request.Naslov)
            || request.Sadrzaj is not null && string.IsNullOrWhiteSpace(request.Sadrzaj)
            || !ValidOptional(request.Kategorija, Categories)
            || !ValidOptional(request.Pouzdanost, Reliability)
            || !ValidOptionalHttpsUrl(request.ImageUrl)
            || !ValidOptionalHttpsUrl(request.OriginalUrl))
            return NewsResult<NewsDetailResponse>.Failure(NewsError.Validation, "Podaci za izmenu vesti nisu validni.");

        var old = post.ToBsonDocument();
        if (request.Naslov is not null) post.Naslov = request.Naslov.Trim();
        if (request.Sadrzaj is not null) post.Sadrzaj = request.Sadrzaj.Trim();
        if (request.Kategorija is not null) post.Kategorija = request.Kategorija;
        if (request.Pouzdanost is not null) post.Pouzdanost = request.Pouzdanost;
        if (request.ImageUrl is not null) post.ImageUrl = PreserveOptionalHttpsUrl(request.ImageUrl);
        if (request.OriginalUrl is not null) post.OriginalUrl = NormalizeOptionalUrl(request.OriginalUrl);
        post.Fingerprint = post.OriginalUrl is null ? null : NewsFingerprint.Create(post.OriginalUrl, post.Naslov);
        post.UpdatedAt = UtcNow();

        try
        {
            if (!await _repository.UpdateAsync(id, post, cancellationToken))
                return NewsResult<NewsDetailResponse>.Failure(NewsError.NotFound, "Vest nije pronadjena.");
        }
        catch (MongoWriteException exception) when (IsDuplicate(exception))
        {
            return NewsResult<NewsDetailResponse>.Failure(NewsError.Conflict, "Vest sa tim izvornim URL-om vec postoji.");
        }

        await AuditAsync(actorId, "post", id, "izmena_vesti", old, post.ToBsonDocument(), cancellationToken);
        return NewsResult<NewsDetailResponse>.Success((await GetDetailAsync(id, cancellationToken))!);
    }

    public async Task<NewsResult<NewsDetailResponse>> DeleteAsync(
        string id,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var post = await _repository.GetVisibleAsync(id, cancellationToken);
        if (post is null)
            return NewsResult<NewsDetailResponse>.Failure(NewsError.NotFound, "Vest nije pronadjena.");
        var old = post.ToBsonDocument();
        post.Obrisan = true;
        post.UpdatedAt = UtcNow();
        if (!await _repository.UpdateAsync(id, post, cancellationToken))
            return NewsResult<NewsDetailResponse>.Failure(NewsError.NotFound, "Vest nije pronadjena.");
        await AuditAsync(actorId, "post", id, "brisanje_vesti", old, post.ToBsonDocument(), cancellationToken);
        var context = await LoadMappingContextAsync([post], cancellationToken);
        return NewsResult<NewsDetailResponse>.Success(MapDetail(post, context));
    }

    public async Task<IReadOnlyList<NewsSourceResponse>> GetSourcesAsync(
        CancellationToken cancellationToken = default) =>
        (await _repository.GetSourcesAsync(cancellationToken)).Select(MapSource).ToList();

    public async Task<NewsResult<NewsSourceResponse>> CreateSourceAsync(
        CreateNewsSourceRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var error = ValidateSource(request);
        if (error is not null) return NewsResult<NewsSourceResponse>.Failure(NewsError.Validation, error);
        var now = UtcNow();
        var source = BuildSource(request, actorId, now);
        try
        {
            await _repository.CreateSourceAsync(source, cancellationToken);
        }
        catch (MongoWriteException exception) when (IsDuplicate(exception))
        {
            return NewsResult<NewsSourceResponse>.Failure(NewsError.Conflict, "Izvor sa tim feed URL-om vec postoji.");
        }
        await AuditAsync(actorId, "news_source", source.Id!, "kreiranje_izvora", null,
            source.ToBsonDocument(), cancellationToken);
        return NewsResult<NewsSourceResponse>.Success(MapSource(source));
    }

    public async Task<NewsResult<NewsSourceResponse>> UpdateSourceAsync(
        string id,
        UpdateNewsSourceRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetSourceAsync(id, cancellationToken);
        if (source is null)
            return NewsResult<NewsSourceResponse>.Failure(NewsError.NotFound, "Izvor nije pronadjen.");
        var error = ValidateSource(request);
        if (error is not null) return NewsResult<NewsSourceResponse>.Failure(NewsError.Validation, error);

        var old = source.ToBsonDocument();
        source.Naziv = request.Naziv.Trim();
        source.FeedUrl = PreserveHttpsUrl(request.FeedUrl);
        source.SiteUrl = PreserveHttpsUrl(request.SiteUrl);
        source.PodrazumevanaKategorija = request.PodrazumevanaKategorija;
        source.PodrazumevanaPouzdanost = request.PodrazumevanaPouzdanost;
        source.UkljuceniPojmovi = NormalizeTerms(request.UkljuceniPojmovi);
        source.IskljuceniPojmovi = NormalizeTerms(request.IskljuceniPojmovi);
        var wasActive = source.Aktivan;
        source.Aktivan = request.Aktivan;
        if (request.Aktivan && !wasActive)
        {
            source.PauziranRazlog = null;
            source.UzastopneGreske = 0;
        }
        else if (!request.Aktivan && wasActive)
        {
            source.PauziranRazlog ??= "Izvor je deaktiviran.";
        }
        source.UpdatedBy = actorId;
        source.UpdatedAt = UtcNow();
        try
        {
            if (!await _repository.UpdateSourceAsync(id, source, cancellationToken))
                return NewsResult<NewsSourceResponse>.Failure(NewsError.NotFound, "Izvor nije pronadjen.");
        }
        catch (MongoWriteException exception) when (IsDuplicate(exception))
        {
            return NewsResult<NewsSourceResponse>.Failure(NewsError.Conflict, "Izvor sa tim feed URL-om vec postoji.");
        }
        await AuditAsync(actorId, "news_source", id, "izmena_izvora", old,
            source.ToBsonDocument(), cancellationToken);
        return NewsResult<NewsSourceResponse>.Success(MapSource(source));
    }

    public Task<NewsResult<NewsSourceResponse>> DeactivateSourceAsync(
        string id, string actorId, CancellationToken cancellationToken = default) =>
        ChangeSourceStateAsync(id, actorId, false, "Izvor je deaktiviran.", "deaktiviranje_izvora", false, cancellationToken);

    public async Task<NewsResult<NewsSourceResponse>> PauseSourceAsync(
        string id,
        PauseNewsSourceRequest request,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Razlog))
            return NewsResult<NewsSourceResponse>.Failure(NewsError.Validation, "Razlog pauziranja je obavezan.");
        return await ChangeSourceStateAsync(id, actorId, false, request.Razlog.Trim(),
            "pauziranje_izvora", false, cancellationToken);
    }

    public Task<NewsResult<NewsSourceResponse>> ResumeSourceAsync(
        string id, string actorId, CancellationToken cancellationToken = default) =>
        ChangeSourceStateAsync(id, actorId, true, null, "nastavak_izvora", true, cancellationToken);

    public async Task<NewsResult<NewsSourceSyncResponse>> SyncSourceAsync(
        string id,
        string actorId,
        CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetSourceAsync(id, cancellationToken);
        if (source is null)
            return NewsResult<NewsSourceSyncResponse>.Failure(NewsError.NotFound, "Izvor nije pronadjen.");
        var response = await _ingestionService.SyncSourceAsync(id, actorId, cancellationToken);
        var updated = await _repository.GetSourceAsync(id, cancellationToken);
        await AuditAsync(actorId, "news_source", id, "sinhronizacija_izvora",
            source.ToBsonDocument(), updated?.ToBsonDocument(), cancellationToken);
        return NewsResult<NewsSourceSyncResponse>.Success(response);
    }

    private async Task<NewsResult<NewsDetailResponse>> CreatePostAsync(
        Post post, string actorId, string action, CancellationToken cancellationToken)
    {
        if (post.OriginalUrl is not null)
        {
            var duplicate = await _repository.FindDuplicateAsync(null, post.OriginalUrl,
                post.Fingerprint!, cancellationToken);
            if (duplicate is not null)
                return NewsResult<NewsDetailResponse>.Failure(NewsError.Conflict, "Vest sa tim izvornim URL-om vec postoji.");
        }
        try
        {
            await _repository.CreateAsync(post, cancellationToken);
        }
        catch (MongoWriteException exception) when (IsDuplicate(exception))
        {
            return NewsResult<NewsDetailResponse>.Failure(NewsError.Conflict, "Vest sa tim izvornim URL-om vec postoji.");
        }
        await AuditAsync(actorId, "post", post.Id!, action, null, post.ToBsonDocument(), cancellationToken);
        return NewsResult<NewsDetailResponse>.Success((await GetDetailAsync(post.Id!, cancellationToken))!);
    }

    private async Task<NewsResult<NewsSourceResponse>> ChangeSourceStateAsync(
        string id, string actorId, bool active, string? reason, string action,
        bool resetErrors, CancellationToken cancellationToken)
    {
        var source = await _repository.GetSourceAsync(id, cancellationToken);
        if (source is null)
            return NewsResult<NewsSourceResponse>.Failure(NewsError.NotFound, "Izvor nije pronadjen.");
        var old = source.ToBsonDocument();
        source.Aktivan = active;
        source.PauziranRazlog = reason;
        if (resetErrors) source.UzastopneGreske = 0;
        source.UpdatedBy = actorId;
        source.UpdatedAt = UtcNow();
        await _repository.UpdateSourceAsync(id, source, cancellationToken);
        await AuditAsync(actorId, "news_source", id, action, old,
            source.ToBsonDocument(), cancellationToken);
        return NewsResult<NewsSourceResponse>.Success(MapSource(source));
    }

    private async Task<MappingContext> LoadMappingContextAsync(
        IReadOnlyCollection<Post> posts,
        CancellationToken cancellationToken)
    {
        var sources = await _repository.GetSourcesAsync(cancellationToken);
        var authorIds = posts.Where(post => post.AutorId is not null)
            .Select(post => post.AutorId!).Distinct().ToList();
        var users = await _forumRepository.GetUsersAsync(authorIds, cancellationToken);
        var comments = await _forumRepository.GetCommentsForPostsAsync(
            posts.Select(post => post.Id!).ToList(), cancellationToken);
        var counts = comments.Where(comment => !comment.Obrisan)
            .GroupBy(comment => comment.PostId)
            .ToDictionary(group => group.Key, group => group.Count());
        return new(sources.Where(source => source.Id is not null).ToDictionary(source => source.Id!), users, counts);
    }

    private static NewsTimelineItemResponse MapTimelineItem(Post post, MappingContext context) => new(
        post.Id!, post.Naslov, Excerpt(post.Sadrzaj), post.Kategorija ?? "premier_league",
        post.Pouzdanost ?? "pouzdan_izvor", post.SourceId,
        SourceName(post.SourceId, context), post.AutorId, Username(post.AutorId, context),
        post.OriginalUrl, post.ImageUrl, post.XEmbedUrl, PublishedAt(post), post.FetchedAt,
        post.UvozAutomatski, context.CommentCounts.GetValueOrDefault(post.Id!));

    private static NewsDetailResponse MapDetail(Post post, MappingContext context) => new(
        post.Id!, post.Naslov, post.Sadrzaj, post.Kategorija ?? "premier_league",
        post.Pouzdanost ?? "pouzdan_izvor", post.SourceId,
        SourceName(post.SourceId, context), post.AutorId, Username(post.AutorId, context),
        post.ExternalAuthor, post.OriginalUrl, post.ImageUrl, post.XEmbedUrl,
        PublishedAt(post), post.FetchedAt, post.UpdatedAt, post.UvozAutomatski,
        context.CommentCounts.GetValueOrDefault(post.Id!));

    private static NewsSourceResponse MapSource(NewsSource source) => new(
        source.Id!, source.Naziv, source.FeedUrl, source.SiteUrl, source.Tip,
        source.PodrazumevanaKategorija, source.PodrazumevanaPouzdanost,
        source.UkljuceniPojmovi, source.IskljuceniPojmovi, source.Aktivan,
        source.PauziranRazlog, source.UzastopneGreske, source.PoslednjaProveraAt,
        source.PoslednjiUspehAt, source.CreatedAt, source.UpdatedAt);

    private static NewsSource BuildSource(CreateNewsSourceRequest request, string actorId, DateTime now) => new()
    {
        Naziv = request.Naziv.Trim(),
        FeedUrl = PreserveHttpsUrl(request.FeedUrl),
        SiteUrl = PreserveHttpsUrl(request.SiteUrl),
        Tip = "rss",
        PodrazumevanaKategorija = request.PodrazumevanaKategorija,
        PodrazumevanaPouzdanost = request.PodrazumevanaPouzdanost,
        UkljuceniPojmovi = NormalizeTerms(request.UkljuceniPojmovi),
        IskljuceniPojmovi = NormalizeTerms(request.IskljuceniPojmovi),
        Aktivan = request.Aktivan,
        CreatedBy = actorId,
        UpdatedBy = actorId,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static string? ValidateArticle(
        string title, string body, string category, string reliability,
        string? imageUrl, string? originalUrl)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            return "Naslov i sadrzaj su obavezni.";
        if (title.Trim().Length > 250 || body.Trim().Length > 20_000)
            return "Naslov ili sadrzaj je predugacak.";
        if (!ValidRequired(category, Categories) || !ValidRequired(reliability, Reliability))
            return "Kategorija ili pouzdanost nije validna.";
        if (!ValidOptionalHttpsUrl(imageUrl) || !ValidOptionalHttpsUrl(originalUrl))
            return "URL mora biti validna HTTPS adresa.";
        return null;
    }

    private static string? ValidateSource(CreateNewsSourceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Naziv) || request.Naziv.Trim().Length > 100)
            return "Naziv izvora je obavezan i moze imati najvise 100 znakova.";
        if (!ValidRequired(request.PodrazumevanaKategorija, Categories)
            || !ValidRequired(request.PodrazumevanaPouzdanost, Reliability))
            return "Kategorija ili pouzdanost izvora nije validna.";
        if (!ValidHttpsUrl(request.FeedUrl) || !ValidHttpsUrl(request.SiteUrl))
            return "Feed i sajt izvora moraju biti validne HTTPS adrese.";
        if (request.UkljuceniPojmovi.Count > 50 || request.IskljuceniPojmovi.Count > 50)
            return "Izvor moze imati najvise 50 ukljucenih i 50 iskljucenih pojmova.";
        return null;
    }

    private async Task AuditAsync(
        string actorId, string targetType, string targetId, string action,
        BsonDocument? oldValue, BsonDocument? newValue, CancellationToken cancellationToken) =>
        await _repository.RecordAuditAsync(new EditorialAuditEvent
        {
            ActorId = actorId,
            TargetType = targetType,
            TargetId = targetId,
            Akcija = action,
            Staro = oldValue,
            Novo = newValue,
            Datum = UtcNow()
        }, cancellationToken);

    private static bool TryNormalizeXUrl(string value, out string normalized)
    {
        normalized = string.Empty;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.Port != 443 || uri.Host is not ("x.com" or "www.x.com")) return false;
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3 || segments[0].Length == 0 || segments[1] != "status"
            || !segments[2].All(char.IsDigit)) return false;
        normalized = $"https://x.com/{segments[0]}/status/{segments[2]}";
        return true;
    }

    private static bool ValidRequired(string value, HashSet<string> allowed) => allowed.Contains(value);
    private static bool ValidOptional(string? value, HashSet<string> allowed) => value is null || allowed.Contains(value);
    private static bool ValidOptionalHttpsUrl(string? value) => string.IsNullOrWhiteSpace(value) || ValidHttpsUrl(value);
    private static bool ValidHttpsUrl(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && uri.Port == 443;
    private static string? NormalizeOptionalUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : NewsFingerprint.NormalizeUrl(value.Trim());
    private static string PreserveHttpsUrl(string value) => new Uri(value.Trim()).AbsoluteUri;
    private static string? PreserveOptionalHttpsUrl(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : PreserveHttpsUrl(value);
    private static List<string> NormalizeTerms(IEnumerable<string> values) => values
        .Select(value => value.Trim()).Where(value => value.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    private static bool IsDuplicate(MongoWriteException exception) =>
        exception.WriteError?.Category == ServerErrorCategory.DuplicateKey;
    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;
    private static DateTime PublishedAt(Post post) =>
        (post.PublishedAt ?? post.FetchedAt ?? post.DatumKreiranja).ToUniversalTime();
    private static string Excerpt(string value) => value.Length <= 500 ? value : $"{value[..497]}...";
    private static string? SourceName(string? sourceId, MappingContext context) =>
        sourceId is not null && context.Sources.TryGetValue(sourceId, out var source) ? source.Naziv : null;
    private static string? Username(string? authorId, MappingContext context) =>
        authorId is not null && context.Users.TryGetValue(authorId, out var user) ? user.Username : null;

    private sealed record MappingContext(
        IReadOnlyDictionary<string, NewsSource> Sources,
        IReadOnlyDictionary<string, User> Users,
        IReadOnlyDictionary<string, int> CommentCounts);
}
