using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/clubs")]
public sealed class ClubsController : ControllerBase
{
    private readonly IClubProfileService _clubs;

    public ClubsController(IClubProfileService clubs)
    {
        _clubs = clubs;
    }

    [HttpGet("{providerId:int}")]
    [ProducesResponseType(typeof(ClubProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ClubProfileDto>> GetAsync(
        int providerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var club = await _clubs.GetAsync(providerId, cancellationToken);
            return club is null ? NotFound() : Ok(club);
        }
        catch (ProfileUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
    }
}
