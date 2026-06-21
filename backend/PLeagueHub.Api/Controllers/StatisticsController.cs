using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/statistics")]
public sealed class StatisticsController : ControllerBase
{
    private readonly IRepository<Statistic> _statisticsRepository;
    private readonly IRepository<Match> _matchesRepository;
    private readonly IRepository<Player> _playersRepository;

    public StatisticsController(
        IRepository<Statistic> statisticsRepository,
        IRepository<Match> matchesRepository,
        IRepository<Player> playersRepository)
    {
        _statisticsRepository = statisticsRepository;
        _matchesRepository = matchesRepository;
        _playersRepository = playersRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<Statistic>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Statistic>>> GetStatisticsAsync(
        [FromQuery] string? matchId,
        [FromQuery] string? playerId,
        CancellationToken cancellationToken)
    {
        var statistics = await _statisticsRepository.GetAllAsync(cancellationToken);
        var filteredStatistics = statistics.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(matchId))
        {
            filteredStatistics = filteredStatistics.Where(statistic =>
                string.Equals(statistic.MatchId, matchId.Trim(), StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(playerId))
        {
            filteredStatistics = filteredStatistics.Where(statistic =>
                string.Equals(statistic.PlayerId, playerId.Trim(), StringComparison.Ordinal));
        }

        return Ok(filteredStatistics
            .OrderBy(statistic => statistic.MatchId)
            .ThenBy(statistic => statistic.PlayerId)
            .ToList());
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Statistic), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Statistic>> GetStatisticByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var statistic = await _statisticsRepository.GetByIdAsync(id, cancellationToken);

        if (statistic is null)
        {
            return NotFound();
        }

        return Ok(statistic);
    }

    [Authorize(Roles = "administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(Statistic), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Statistic>> CreateStatisticAsync(
        CreateStatisticRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidStatisticRequest(
            request.MatchId,
            request.PlayerId,
            request.Golovi,
            request.Asistencije,
            request.Kartoni,
            request.MinutiIgre,
            request.Ocena))
        {
            return BadRequest(new { message = "MatchId, playerId, minuti igre i ocena su obavezni." });
        }

        var matchId = request.MatchId.Trim();
        var playerId = request.PlayerId.Trim();

        if (!await LinkedDocumentsExistAsync(matchId, playerId, cancellationToken))
        {
            return BadRequest(new { message = "Utakmica i igrac moraju postojati." });
        }

        var statistic = new Statistic
        {
            MatchId = matchId,
            PlayerId = playerId,
            Golovi = request.Golovi,
            Asistencije = request.Asistencije,
            Kartoni = request.Kartoni,
            MinutiIgre = request.MinutiIgre,
            Ocena = request.Ocena
        };

        var createdStatistic = await _statisticsRepository.CreateAsync(statistic, cancellationToken);

        return Created($"/api/statistics/{createdStatistic.Id}", createdStatistic);
    }

    [Authorize(Roles = "administrator")]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Statistic), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Statistic>> UpdateStatisticAsync(
        string id,
        UpdateStatisticRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidStatisticRequest(
            request.MatchId,
            request.PlayerId,
            request.Golovi,
            request.Asistencije,
            request.Kartoni,
            request.MinutiIgre,
            request.Ocena))
        {
            return BadRequest(new { message = "MatchId, playerId, minuti igre i ocena su obavezni." });
        }

        var statistic = await _statisticsRepository.GetByIdAsync(id, cancellationToken);

        if (statistic is null)
        {
            return NotFound();
        }

        var matchId = request.MatchId.Trim();
        var playerId = request.PlayerId.Trim();

        if (!await LinkedDocumentsExistAsync(matchId, playerId, cancellationToken))
        {
            return BadRequest(new { message = "Utakmica i igrac moraju postojati." });
        }

        statistic.MatchId = matchId;
        statistic.PlayerId = playerId;
        statistic.Golovi = request.Golovi;
        statistic.Asistencije = request.Asistencije;
        statistic.Kartoni = request.Kartoni;
        statistic.MinutiIgre = request.MinutiIgre;
        statistic.Ocena = request.Ocena;

        var updated = await _statisticsRepository.UpdateAsync(id, statistic, cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return Ok(statistic);
    }

    [Authorize(Roles = "administrator")]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStatisticAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var deleted = await _statisticsRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private async Task<bool> LinkedDocumentsExistAsync(
        string matchId,
        string playerId,
        CancellationToken cancellationToken)
    {
        var match = await _matchesRepository.GetByIdAsync(matchId, cancellationToken);
        var player = await _playersRepository.GetByIdAsync(playerId, cancellationToken);

        return match is not null && player is not null;
    }

    private static bool IsValidStatisticRequest(
        string matchId,
        string playerId,
        int golovi,
        int asistencije,
        int kartoni,
        int minutiIgre,
        double ocena)
    {
        return !string.IsNullOrWhiteSpace(matchId)
            && !string.IsNullOrWhiteSpace(playerId)
            && golovi >= 0
            && asistencije >= 0
            && kartoni >= 0
            && minutiIgre is >= 0 and <= 130
            && ocena is >= 0 and <= 10;
    }
}
