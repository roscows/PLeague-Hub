using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchDetailController : ControllerBase
{
    private readonly IMatchDetailService _matchDetailService;

    public MatchDetailController(IMatchDetailService matchDetailService)
    {
        _matchDetailService = matchDetailService;
    }

    [HttpGet("{matchId}/detail")]
    [ProducesResponseType(typeof(MatchDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<MatchDetailResponse>> GetDetailAsync(
        string matchId,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = await _matchDetailService.GetAsync(matchId, cancellationToken);

            if (detail is null)
            {
                return NotFound();
            }

            return Ok(detail);
        }
        catch (MatchDetailUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
    }
}
