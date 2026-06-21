using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/news")]
public sealed class NewsController : ControllerBase
{
    private readonly INewsService _newsService;
    private readonly ICommentService _commentService;

    public NewsController(INewsService newsService, ICommentService commentService)
    {
        _newsService = newsService;
        _commentService = commentService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(NewsTimelineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NewsTimelineResponse>> GetNewsAsync(
        [FromQuery] NewsTimelineRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _newsService.GetTimelineAsync(request, cancellationToken);
        return result.Error == NewsError.None ? Ok(result.Value) : MapError(result.Error, result.Message);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(NewsDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NewsDetailResponse>> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var news = await _newsService.GetDetailAsync(id, cancellationToken);
        return news is null ? NotFound(new { message = "Vest nije pronadjena." }) : Ok(news);
    }

    [Authorize(Roles = "moderator,administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(NewsDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NewsDetailResponse>> CreateNewsAsync(
        CreateNewsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _newsService.CreateAsync(request, ActorId(), cancellationToken);
        return result.Error == NewsError.None
            ? Created($"/api/news/{result.Value!.Id}", result.Value)
            : MapError(result.Error, result.Message);
    }

    [Authorize(Roles = "moderator,administrator")]
    [HttpPost("x")]
    [ProducesResponseType(typeof(NewsDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NewsDetailResponse>> CreateXNewsAsync(
        CreateXNewsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _newsService.CreateXAsync(request, ActorId(), cancellationToken);
        return result.Error == NewsError.None
            ? Created($"/api/news/{result.Value!.Id}", result.Value)
            : MapError(result.Error, result.Message);
    }

    [Authorize(Roles = "moderator,administrator")]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(NewsDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NewsDetailResponse>> UpdateNewsAsync(
        string id,
        UpdateNewsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _newsService.UpdateAsync(id, request, ActorId(), cancellationToken);
        return result.Error == NewsError.None ? Ok(result.Value) : MapError(result.Error, result.Message);
    }

    [Authorize(Roles = "moderator,administrator")]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNewsAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _newsService.DeleteAsync(id, ActorId(), cancellationToken);
        return result.Error == NewsError.None ? NoContent() : MapError(result.Error, result.Message);
    }

    [HttpGet("{id}/comments")]
    [ProducesResponseType(typeof(IReadOnlyList<ForumCommentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ForumCommentResponse>>> GetCommentsAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var comments = await _commentService.GetCommentsAsync(
            id, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
        return comments is null ? NotFound(new { message = "Vest nije pronadjena." }) : Ok(comments);
    }

    [Authorize]
    [HttpPost("{id}/comments")]
    [ProducesResponseType(typeof(ForumCommentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ForumCommentResponse>> CreateCommentAsync(
        string id,
        CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _commentService.CreateAsync(
            id, request, User.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken);
        return result.Error == ForumError.None
            ? Created($"/api/news/{id}/comments/{result.Value!.Id}", result.Value)
            : MapForumError(result.Error, result.Message);
    }

    private string ActorId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private ActionResult MapError(NewsError error, string? message)
    {
        var body = new { message = message ?? "Zahtev nije moguce obraditi." };
        return error switch
        {
            NewsError.NotFound => NotFound(body),
            NewsError.Conflict => Conflict(body),
            _ => BadRequest(body)
        };
    }

    private ActionResult MapForumError(ForumError error, string? message)
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
