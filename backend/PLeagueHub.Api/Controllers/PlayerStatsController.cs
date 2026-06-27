using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/player-stats")]
public sealed class PlayerStatsController : ControllerBase
{
    private readonly IRepository<PlayerSeasonStatDocument> _stats;

    public PlayerStatsController(IRepository<PlayerSeasonStatDocument> stats)
    {
        _stats = stats;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<PlayerStatDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PlayerStatDto>>> GetAsync(
        [FromQuery] string? season,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(season))
        {
            return Ok(Array.Empty<PlayerStatDto>());
        }

        var all = await _stats.GetAllAsync(cancellationToken);

        var rows = all
            .Where(stat => string.Equals(stat.Sezona, season, StringComparison.Ordinal))
            .OrderByDescending(stat => stat.Golovi)
            .ThenByDescending(stat => stat.Asistencije)
            .Select((stat, index) => new PlayerStatDto(
                index + 1,
                stat.ProviderId,
                stat.Ime,
                stat.TeamNaziv,
                stat.TeamLogoUrl,
                stat.Golovi,
                stat.Asistencije,
                stat.Odigrano))
            .ToArray();

        return Ok(rows);
    }
}
