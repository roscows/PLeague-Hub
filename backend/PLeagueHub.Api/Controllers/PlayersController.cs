using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/players")]
public sealed class PlayersController : ControllerBase
{
    private readonly IPlayerProfileService _profiles;

    public PlayersController(IPlayerProfileService profiles)
    {
        _profiles = profiles;
    }

    [HttpGet("{providerId:int}")]
    [ProducesResponseType(typeof(PlayerProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PlayerProfileDto>> GetAsync(
        int providerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _profiles.GetAsync(providerId, cancellationToken);
            return profile is null ? NotFound() : Ok(profile);
        }
        catch (ProfileUnavailableException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
    }
}
