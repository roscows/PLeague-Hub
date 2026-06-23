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
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyCollection<StandingRowResponse>>> GetStandingsAsync(
        [FromQuery] int seasonId = StandingsService.CurrentSeasonId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _standingsService.GetStandingsAsync(seasonId, cancellationToken);
            return Ok(rows);
        }
        catch (StandingsUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
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
