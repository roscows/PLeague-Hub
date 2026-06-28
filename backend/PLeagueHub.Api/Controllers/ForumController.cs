using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/forum")]
public sealed class ForumController : ControllerBase
{
    private readonly IForumService _forumService;
    private readonly ICommentReportService _commentReportService;

    public ForumController(IForumService forumService, ICommentReportService commentReportService)
    {
        _forumService = forumService;
        _commentReportService = commentReportService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ForumTopicResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ForumTopicResponse>>> GetDiscussionsAsync(
        [FromQuery] ForumListRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _forumService.GetTopicsAsync(request, cancellationToken));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ForumDiscussionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ForumDiscussionResponse>> GetDiscussionByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var discussion = await _forumService.GetDiscussionAsync(id, cancellationToken);
        return discussion is null ? NotFound() : Ok(discussion);
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(ForumDiscussionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ForumDiscussionResponse>> CreateDiscussionAsync(
        CreateForumPostRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _forumService.CreateDiscussionAsync(
            request,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);

        if (result.Error != ForumError.None)
        {
            return MapError(result.Error, result.Message);
        }

        return Created($"/api/forum/{result.Value!.Id}", result.Value);
    }

    [HttpGet("{id}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<ForumCommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ForumCommentResponse>>> GetCommentsAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var comments = await _forumService.GetCommentsAsync(
            id,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);
        return comments is null ? NotFound() : Ok(comments);
    }

    [Authorize]
    [HttpPost("{id}/comments")]
    [ProducesResponseType(typeof(ForumCommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ForumCommentResponse>> CreateCommentAsync(
        string id,
        CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _forumService.CreateCommentAsync(
            id,
            request,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);

        if (result.Error != ForumError.None)
        {
            return MapError(result.Error, result.Message);
        }

        return Created($"/api/forum/{id}/comments/{result.Value!.Id}", result.Value);
    }

    [Authorize]
    [HttpPut("comments/{commentId}/vote")]
    [ProducesResponseType(typeof(ForumVoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ForumVoteResponse>> VoteAsync(
        string commentId,
        VoteCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _forumService.VoteAsync(
            commentId,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            request.Value,
            cancellationToken);
        return result.Error == ForumError.None
            ? Ok(result.Value)
            : MapError(result.Error, result.Message);
    }

    [Authorize]
    [HttpDelete("comments/{commentId}/vote")]
    [ProducesResponseType(typeof(ForumVoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ForumVoteResponse>> RemoveVoteAsync(
        string commentId,
        CancellationToken cancellationToken)
    {
        var result = await _forumService.RemoveVoteAsync(
            commentId,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            cancellationToken);
        return result.Error == ForumError.None
            ? Ok(result.Value)
            : MapError(result.Error, result.Message);
    }

    [Authorize]
    [HttpPost("comments/{commentId}/report")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReportCommentAsync(
        string commentId,
        CreateCommentReportRequest request,
        CancellationToken cancellationToken)
    {
        var reporterId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(reporterId))
        {
            return Unauthorized();
        }

        var result = await _commentReportService.CreateAsync(commentId, reporterId, request, cancellationToken);

        return result switch
        {
            ReportCreateResult.Created => StatusCode(
                StatusCodes.Status201Created,
                new { message = "Prijava je zabelezena." }),
            ReportCreateResult.DuplicatePending => Ok(new { message = "Vec ste prijavili ovaj komentar." }),
            ReportCreateResult.CommentNotFound => NotFound(new { message = "Komentar nije pronadjen." }),
            ReportCreateResult.CannotReportOwn => BadRequest(new { message = "Ne mozete prijaviti sopstveni komentar." }),
            _ => BadRequest(new { message = "Neispravna kategorija prijave." })
        };
    }

    private ActionResult MapError(ForumError error, string? message)
    {
        var body = new { message = message ?? "Zahtev nije moguce obraditi." };
        return error switch
        {
            ForumError.Unauthorized => Unauthorized(body),
            ForumError.NotFound => NotFound(body),
            ForumError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, body),
            _ => BadRequest(body)
        };
    }
}
