using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Authorize(Roles = "moderator,administrator")]
[Route("api/moderation")]
public sealed class ModerationController : ControllerBase
{
    private readonly IModerationRepository _repository;
    private readonly IModerationService _service;
    private readonly ICommentReportService _reportService;
    private readonly IStaffNoticeService _noticeService;
    private readonly IModerationPanelService _panelService;
    private readonly TimeProvider _timeProvider;

    public ModerationController(
        IModerationRepository repository,
        IModerationService service,
        ICommentReportService reportService,
        IStaffNoticeService noticeService,
        IModerationPanelService panelService,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _service = service;
        _reportService = reportService;
        _noticeService = noticeService;
        _panelService = panelService;
        _timeProvider = timeProvider;
    }

    [HttpGet("activity")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ModerationActivityDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ModerationActivityDto>>> GetActivityAsync(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
        => Ok(await _panelService.GetActivityAsync(limit, cancellationToken));

    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyCollection<PanelUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PanelUserDto>>> GetUsersAsync(
        [FromQuery] string? q,
        [FromQuery] bool staffOnly = false,
        CancellationToken cancellationToken = default)
        => Ok(await _panelService.SearchUsersAsync(q, staffOnly, cancellationToken));

    [HttpPut("users/{id}/role")]
    [ProducesResponseType(typeof(PanelUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeRoleAsync(
        string id,
        ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        var (result, user) = await _panelService.ChangeRoleAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            id,
            request.Uloga,
            cancellationToken);

        return result switch
        {
            RoleChangeResult.Changed => Ok(user),
            RoleChangeResult.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = "Samo administrator moze menjati uloge." }),
            RoleChangeResult.NotFound => NotFound(),
            RoleChangeResult.InvalidRole => BadRequest(new { message = "Nedozvoljena uloga." }),
            RoleChangeResult.SelfChange => BadRequest(new { message = "Ne mozete promeniti sopstvenu ulogu." }),
            _ => BadRequest(new { message = "Ne mozete menjati ulogu administratora." })
        };
    }

    [HttpGet("notices")]
    [ProducesResponseType(typeof(IReadOnlyCollection<StaffNoticeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<StaffNoticeDto>>> GetNoticesAsync(CancellationToken cancellationToken)
        => Ok(await _noticeService.GetAllAsync(cancellationToken));

    [HttpPost("notices")]
    [ProducesResponseType(typeof(StaffNoticeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StaffNoticeDto>> CreateNoticeAsync(
        CreateStaffNoticeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Tekst))
        {
            return BadRequest(new { message = "Tekst obavestenja je obavezan." });
        }

        var notice = await _noticeService.CreateAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            request.Tekst,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, notice);
    }

    [HttpDelete("notices/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNoticeAsync(string id, CancellationToken cancellationToken)
        => await _noticeService.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("notices/{id}/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PinNoticeAsync(string id, CancellationToken cancellationToken)
        => await _noticeService.SetPinnedAsync(id, true, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("notices/{id}/pin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnpinNoticeAsync(string id, CancellationToken cancellationToken)
        => await _noticeService.SetPinnedAsync(id, false, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("reports")]
    [ProducesResponseType(typeof(IReadOnlyCollection<CommentReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<CommentReportDto>>> GetReportsAsync(
        CancellationToken cancellationToken)
    {
        return Ok(await _reportService.GetPendingAsync(cancellationToken));
    }

    [HttpPost("reports/{id}/resolve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveReportAsync(
        string id,
        ResolveReportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reportService.ResolveAsync(
            id,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            request.Akcija,
            cancellationToken);

        return result switch
        {
            ReportResolveResult.Resolved => NoContent(),
            ReportResolveResult.NotFound => NotFound(),
            _ => BadRequest(new { message = "Neispravna akcija." })
        };
    }

    [HttpPost("users/{id}/actions")]
    [ProducesResponseType(typeof(ModerationStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModerationStateResponse>> ApplyUserActionAsync(
        string id,
        CreateModerationActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ApplyAsync(
            id,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            request,
            cancellationToken);
        return result.Error == ModerationError.None
            ? Ok(result.Value)
            : MapError(result.Error, result.Message);
    }

    [HttpGet("users/{id}/state")]
    [ProducesResponseType(typeof(ModerationStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModerationStateResponse>> GetUserStateAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var target = await _repository.GetUserAsync(id, cancellationToken);
        if (target is null) return NotFound();
        if (!await CanModerateAsync(target.Id!, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nemate dozvolu da moderirate ovog korisnika." });

        var state = await _service.GetActiveStateAsync(id, cancellationToken);
        return state is null ? NoContent() : Ok(state);
    }

    [HttpDelete("users/{id}/action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeUserActionAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _service.RevokeAsync(
            id,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);
        return result.Error == ModerationError.None
            ? NoContent()
            : MapError(result.Error, result.Message);
    }

    [HttpDelete("posts/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DeletePostAsync(string id, CancellationToken cancellationToken) =>
        ModeratePostAsync(id, delete: true, pin: null, cancellationToken);

    [HttpDelete("comments/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCommentAsync(string id, CancellationToken cancellationToken)
    {
        var comment = await _repository.GetCommentAsync(id, cancellationToken);
        if (comment is null || comment.Obrisan) return NotFound();
        if (!await CanModerateAsync(comment.AutorId, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nemate dozvolu da obrisete ovaj komentar." });
        return await _repository.SetCommentDeletedAsync(id, cancellationToken) ? NoContent() : NotFound();
    }

    [HttpPut("posts/{id}/pin")]
    public Task<IActionResult> PinPostAsync(string id, CancellationToken cancellationToken) =>
        ModeratePostAsync(id, delete: false, pin: true, cancellationToken);

    [HttpDelete("posts/{id}/pin")]
    public Task<IActionResult> UnpinPostAsync(string id, CancellationToken cancellationToken) =>
        ModeratePostAsync(id, delete: false, pin: false, cancellationToken);

    [HttpPut("comments/{id}/pin")]
    public Task<IActionResult> PinCommentAsync(string id, CancellationToken cancellationToken) =>
        SetCommentPinAsync(id, true, cancellationToken);

    [HttpDelete("comments/{id}/pin")]
    public Task<IActionResult> UnpinCommentAsync(string id, CancellationToken cancellationToken) =>
        SetCommentPinAsync(id, false, cancellationToken);

    private async Task<IActionResult> ModeratePostAsync(
        string id,
        bool delete,
        bool? pin,
        CancellationToken cancellationToken)
    {
        var post = await _repository.GetPostAsync(id, cancellationToken);
        if (post is null || post.Obrisan) return NotFound();
        if (post.AutorId is not null && !await CanModerateAsync(post.AutorId, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nemate dozvolu da moderirate ovu temu." });

        var updated = delete
            ? await _repository.SetPostDeletedAsync(id, cancellationToken)
            : await _repository.SetPostPinnedAsync(
                id,
                pin!.Value,
                User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                _timeProvider.GetUtcNow().UtcDateTime,
                cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    private async Task<IActionResult> SetCommentPinAsync(
        string id,
        bool pinned,
        CancellationToken cancellationToken)
    {
        var comment = await _repository.GetCommentAsync(id, cancellationToken);
        if (comment is null || comment.Obrisan) return NotFound();
        if (!await CanModerateAsync(comment.AutorId, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Nemate dozvolu da moderirate ovaj komentar." });

        var updated = await _repository.SetCommentPinnedAsync(
            id,
            pinned,
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            _timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    private Task<bool> CanModerateAsync(string authorId, CancellationToken cancellationToken) =>
        _service.CanModerateContentAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            authorId,
            cancellationToken);

    private ObjectResult MapError(ModerationError error, string? message)
    {
        var status = error switch
        {
            ModerationError.Unauthorized => StatusCodes.Status401Unauthorized,
            ModerationError.Forbidden => StatusCodes.Status403Forbidden,
            ModerationError.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, new { message = message ?? "Zahtev nije moguce obraditi." });
    }
}
