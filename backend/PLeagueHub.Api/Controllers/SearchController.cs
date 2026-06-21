using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly SearchService _searchService;

    public SearchController(SearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<SearchResultResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<SearchResultResponse>>> SearchAsync(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] int limit = 8,
        CancellationToken cancellationToken = default)
    {
        var results = await _searchService.SearchAsync(query, limit, cancellationToken);
        return Ok(results);
    }
}
