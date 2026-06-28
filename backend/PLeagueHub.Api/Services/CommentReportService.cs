using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public enum ReportCreateResult
{
    Created,
    DuplicatePending,
    CommentNotFound,
    CannotReportOwn,
    InvalidCategory
}

public enum ReportResolveResult
{
    Resolved,
    NotFound,
    InvalidAction
}

public interface ICommentReportService
{
    Task<ReportCreateResult> CreateAsync(
        string commentId,
        string reporterId,
        CreateCommentReportRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CommentReportDto>> GetPendingAsync(CancellationToken cancellationToken);

    Task<ReportResolveResult> ResolveAsync(
        string reportId,
        string moderatorId,
        string akcija,
        CancellationToken cancellationToken);
}

public sealed class CommentReportService : ICommentReportService
{
    private const string StatusPending = "na_cekanju";
    private const string StatusResolved = "reseno";
    private const string StatusDismissed = "odbaceno";

    private static readonly HashSet<string> AllowedCategories =
        new(StringComparer.Ordinal) { "spam", "uvrede", "offtopic", "ostalo" };

    private readonly IRepository<CommentReportDocument> _reports;
    private readonly IModerationRepository _moderation;
    private readonly IRepository<User> _users;
    private readonly TimeProvider _timeProvider;

    public CommentReportService(
        IRepository<CommentReportDocument> reports,
        IModerationRepository moderation,
        IRepository<User> users,
        TimeProvider timeProvider)
    {
        _reports = reports;
        _moderation = moderation;
        _users = users;
        _timeProvider = timeProvider;
    }

    public async Task<ReportCreateResult> CreateAsync(
        string commentId,
        string reporterId,
        CreateCommentReportRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Kategorija) || !AllowedCategories.Contains(request.Kategorija))
        {
            return ReportCreateResult.InvalidCategory;
        }

        var comment = await _moderation.GetCommentAsync(commentId, cancellationToken);

        if (comment is null || comment.Obrisan)
        {
            return ReportCreateResult.CommentNotFound;
        }

        if (string.Equals(comment.AutorId, reporterId, StringComparison.Ordinal))
        {
            return ReportCreateResult.CannotReportOwn;
        }

        var existing = await _reports.GetAllAsync(cancellationToken);
        var alreadyPending = existing.Any(report =>
            report.KomentarId == commentId
            && report.PrijavioId == reporterId
            && report.Status == StatusPending);

        if (alreadyPending)
        {
            return ReportCreateResult.DuplicatePending;
        }

        await _reports.CreateAsync(
            new CommentReportDocument
            {
                KomentarId = commentId,
                PostId = comment.PostId,
                PrijavioId = reporterId,
                Kategorija = request.Kategorija,
                Opis = request.Opis?.Trim() ?? string.Empty,
                Status = StatusPending,
                DatumPrijave = _timeProvider.GetUtcNow().UtcDateTime
            },
            cancellationToken);

        return ReportCreateResult.Created;
    }

    public async Task<IReadOnlyCollection<CommentReportDto>> GetPendingAsync(CancellationToken cancellationToken)
    {
        var pending = (await _reports.GetAllAsync(cancellationToken))
            .Where(report => report.Status == StatusPending)
            .OrderByDescending(report => report.DatumPrijave)
            .ToList();

        var users = (await _users.GetAllAsync(cancellationToken))
            .Where(user => !string.IsNullOrEmpty(user.Id))
            .GroupBy(user => user.Id!)
            .ToDictionary(group => group.Key, group => group.First());

        var result = new List<CommentReportDto>();

        foreach (var report in pending)
        {
            var comment = await _moderation.GetCommentAsync(report.KomentarId, cancellationToken);

            if (comment is null || comment.Obrisan)
            {
                continue;
            }

            users.TryGetValue(comment.AutorId, out var author);
            users.TryGetValue(report.PrijavioId, out var reporter);

            result.Add(new CommentReportDto(
                report.Id ?? string.Empty,
                report.KomentarId,
                report.PostId,
                comment.Tekst,
                comment.AutorId,
                author?.Username ?? "Nepoznat",
                author?.Uloga ?? "registrovani",
                reporter?.Username ?? "Nepoznat",
                report.Kategorija,
                report.Opis,
                report.DatumPrijave));
        }

        return result;
    }

    public async Task<ReportResolveResult> ResolveAsync(
        string reportId,
        string moderatorId,
        string akcija,
        CancellationToken cancellationToken)
    {
        var report = await _reports.GetByIdAsync(reportId, cancellationToken);

        if (report is null)
        {
            return ReportResolveResult.NotFound;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        switch (akcija)
        {
            case "obrisi":
                await _moderation.SetCommentDeletedAsync(report.KomentarId, cancellationToken);
                var siblings = (await _reports.GetAllAsync(cancellationToken))
                    .Where(item => item.KomentarId == report.KomentarId && item.Status == StatusPending);

                foreach (var sibling in siblings)
                {
                    sibling.Status = StatusResolved;
                    sibling.Ishod = "komentar_obrisan";
                    sibling.ResioId = moderatorId;
                    sibling.ResenoAt = now;
                    await _reports.UpdateAsync(sibling.Id!, sibling, cancellationToken);
                }

                return ReportResolveResult.Resolved;

            case "odbaci":
                report.Status = StatusDismissed;
                report.Ishod = "odbaceno";
                report.ResioId = moderatorId;
                report.ResenoAt = now;
                await _reports.UpdateAsync(report.Id!, report, cancellationToken);
                return ReportResolveResult.Resolved;

            default:
                return ReportResolveResult.InvalidAction;
        }
    }
}
