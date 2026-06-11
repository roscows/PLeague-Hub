using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class StatsController : ControllerBase
{
    private readonly IRepository<Player> _playersRepository;
    private readonly IRepository<Team> _teamsRepository;

    public StatsController(
        IRepository<Player> playersRepository,
        IRepository<Team> teamsRepository)
    {
        _playersRepository = playersRepository;
        _teamsRepository = teamsRepository;
    }

    [HttpGet("players")]
    [ProducesResponseType(typeof(IReadOnlyCollection<Player>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Player>>> GetPlayersAsync(
        CancellationToken cancellationToken)
    {
        var players = await _playersRepository.GetAllAsync(cancellationToken);
        return Ok(players);
    }

    [HttpGet("players/{id}")]
    [ProducesResponseType(typeof(Player), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Player>> GetPlayerByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var player = await _playersRepository.GetByIdAsync(id, cancellationToken);

        if (player is null)
        {
            return NotFound();
        }

        return Ok(player);
    }

    [HttpGet("teams")]
    [ProducesResponseType(typeof(IReadOnlyCollection<Team>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Team>>> GetTeamsAsync(
        CancellationToken cancellationToken)
    {
        var teams = await _teamsRepository.GetAllAsync(cancellationToken);
        return Ok(teams);
    }

    [HttpGet("teams/{id}")]
    [ProducesResponseType(typeof(Team), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Team>> GetTeamByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var team = await _teamsRepository.GetByIdAsync(id, cancellationToken);

        if (team is null)
        {
            return NotFound();
        }

        return Ok(team);
    }
}
