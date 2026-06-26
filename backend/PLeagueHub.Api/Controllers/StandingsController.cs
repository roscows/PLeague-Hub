using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/standings")]
public sealed class StandingsController : ControllerBase
{
    private readonly IStandingsService _standingsService;

    public StandingsController(IStandingsService standingsService)
    {
        _standingsService = standingsService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<StandingRowResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<StandingRowResponse>>> GetStandingsAsync(
        [FromQuery] string? season,
        CancellationToken cancellationToken = default)
    {
        var rows = await _standingsService.GetStandingsAsync(season ?? string.Empty, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("seasons")]
    [ProducesResponseType(typeof(IReadOnlyCollection<SeasonResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<SeasonResponse>>> GetSeasonsAsync(
        CancellationToken cancellationToken = default)
    {
        var seasons = await _standingsService.GetSeasonsAsync(cancellationToken);
        return Ok(seasons);
    }
}
