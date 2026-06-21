using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services;
using PLeagueHub.Api.Services.News;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Authorize(Roles = "moderator,administrator")]
[Route("api/news/sources")]
public sealed class NewsSourcesController : ControllerBase
{
    private readonly INewsService _newsService;

    public NewsSourcesController(INewsService newsService)
    {
        _newsService = newsService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NewsSourceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NewsSourceResponse>>> GetSourcesAsync(
        CancellationToken cancellationToken) =>
        Ok(await _newsService.GetSourcesAsync(cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(NewsSourceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NewsSourceResponse>> CreateSourceAsync(
        CreateNewsSourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _newsService.CreateSourceAsync(request, ActorId(), cancellationToken);
        return result.Error == NewsError.None
            ? Created($"/api/news/sources/{result.Value!.Id}", result.Value)
            : MapError(result.Error, result.Message);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(NewsSourceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NewsSourceResponse>> UpdateSourceAsync(
        string id,
        UpdateNewsSourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _newsService.UpdateSourceAsync(id, request, ActorId(), cancellationToken);
        return result.Error == NewsError.None ? Ok(result.Value) : MapError(result.Error, result.Message);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateSourceAsync(string id, CancellationToken cancellationToken)
    {
        var result = await _newsService.DeactivateSourceAsync(id, ActorId(), cancellationToken);
        return result.Error == NewsError.None ? NoContent() : MapError(result.Error, result.Message);
    }

    [HttpPut("{id}/pause")]
    [ProducesResponseType(typeof(NewsSourceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NewsSourceResponse>> PauseSourceAsync(
        string id,
        PauseNewsSourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _newsService.PauseSourceAsync(id, request, ActorId(), cancellationToken);
        return result.Error == NewsError.None ? Ok(result.Value) : MapError(result.Error, result.Message);
    }

    [HttpDelete("{id}/pause")]
    [ProducesResponseType(typeof(NewsSourceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NewsSourceResponse>> ResumeSourceAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _newsService.ResumeSourceAsync(id, ActorId(), cancellationToken);
        return result.Error == NewsError.None ? Ok(result.Value) : MapError(result.Error, result.Message);
    }

    [HttpPost("{id}/sync")]
    [ProducesResponseType(typeof(NewsSourceSyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NewsSourceSyncResponse>> SyncSourceAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await _newsService.SyncSourceAsync(id, ActorId(), cancellationToken);
        return result.Error == NewsError.None ? Ok(result.Value) : MapError(result.Error, result.Message);
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
}
