using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Authorize(Roles = "administrator")]
[Route("api/integrations/football")]
public sealed class IntegrationsController : ControllerBase
{
    private readonly TeamSyncService _teamSyncService;
    private readonly TeamLogoSyncService _teamLogoSyncService;

    public IntegrationsController(
        TeamSyncService teamSyncService,
        TeamLogoSyncService teamLogoSyncService)
    {
        _teamSyncService = teamSyncService;
        _teamLogoSyncService = teamLogoSyncService;
    }

    [HttpPost("sync/teams")]
    [ProducesResponseType(typeof(TeamSyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<TeamSyncResponse>> SyncTeamsAsync(
        [FromQuery] int seasonId = 96668,
        CancellationToken cancellationToken = default)
    {
        if (seasonId <= 0)
        {
            return BadRequest(new { message = "seasonId must be greater than zero." });
        }

        try
        {
            var result = await _teamSyncService.SyncAsync(seasonId, cancellationToken);
            return Ok(result);
        }
        catch (TeamSyncException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
    }

    [HttpPost("sync/team-logos")]
    [ProducesResponseType(typeof(TeamLogoSyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<TeamLogoSyncResponse>> SyncTeamLogosAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _teamLogoSyncService.SyncAsync(cancellationToken);
            return Ok(result);
        }
        catch (TeamLogoSyncException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = exception.Message });
        }
    }
}
