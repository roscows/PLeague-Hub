using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchesController : ControllerBase
{
    private readonly IRepository<Match> _matchesRepository;

    public MatchesController(IRepository<Match> matchesRepository)
    {
        _matchesRepository = matchesRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<Match>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Match>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var matches = await _matchesRepository.GetAllAsync(cancellationToken);
        return Ok(matches);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Match), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Match>> GetByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var match = await _matchesRepository.GetByIdAsync(id, cancellationToken);

        if (match is null)
        {
            return NotFound();
        }

        return Ok(match);
    }
}
