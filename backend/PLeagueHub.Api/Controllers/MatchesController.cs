using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchesController : ControllerBase
{
    private readonly IRepository<Match> _matchesRepository;
    private readonly IRepository<Team> _teamsRepository;

    public MatchesController(
        IRepository<Match> matchesRepository,
        IRepository<Team> teamsRepository)
    {
        _matchesRepository = matchesRepository;
        _teamsRepository = teamsRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<Match>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Match>>> GetAllAsync(
        [FromQuery] string? season,
        [FromQuery] string? status,
        [FromQuery] int? round,
        CancellationToken cancellationToken)
    {
        var matches = await _matchesRepository.GetAllAsync(cancellationToken);
        var filteredMatches = matches.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(season))
        {
            filteredMatches = filteredMatches.Where(match =>
                string.Equals(match.Sezona, season.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            filteredMatches = filteredMatches.Where(match =>
                string.Equals(match.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (round.HasValue)
        {
            filteredMatches = filteredMatches.Where(match => match.Kolo == round.Value);
        }

        return Ok(filteredMatches.OrderBy(match => match.Datum).ToList());
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

    [Authorize(Roles = "administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(Match), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Match>> CreateMatchAsync(
        CreateMatchRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidMatchRequest(
            request.DomacinId,
            request.GostId,
            request.Kolo,
            request.Sezona,
            request.Status,
            request.GolDomacin,
            request.GolGost))
        {
            return BadRequest(new { message = "Domacin, gost, kolo, sezona i status su obavezni." });
        }

        var domacinId = request.DomacinId.Trim();
        var gostId = request.GostId.Trim();

        if (string.Equals(domacinId, gostId, StringComparison.Ordinal))
        {
            return BadRequest(new { message = "Domacin i gost moraju biti razliciti timovi." });
        }

        var domacin = await _teamsRepository.GetByIdAsync(domacinId, cancellationToken);
        var gost = await _teamsRepository.GetByIdAsync(gostId, cancellationToken);

        if (domacin is null || gost is null)
        {
            return BadRequest(new { message = "Domacin i gost moraju biti postojeci timovi." });
        }

        var match = new Match
        {
            DomacinId = domacinId,
            GostId = gostId,
            Datum = request.Datum,
            Kolo = request.Kolo,
            Sezona = request.Sezona.Trim(),
            GolDomacin = request.GolDomacin,
            GolGost = request.GolGost,
            Status = request.Status.Trim()
        };

        var createdMatch = await _matchesRepository.CreateAsync(match, cancellationToken);

        return Created($"/api/matches/{createdMatch.Id}", createdMatch);
    }

    [Authorize(Roles = "administrator")]
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Match), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Match>> UpdateMatchAsync(
        string id,
        UpdateMatchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Kolo <= 0 || string.IsNullOrWhiteSpace(request.Sezona) || string.IsNullOrWhiteSpace(request.Status))
        {
            return BadRequest(new { message = "Kolo, sezona i status su obavezni." });
        }

        var match = await _matchesRepository.GetByIdAsync(id, cancellationToken);

        if (match is null)
        {
            return NotFound();
        }

        match.Datum = request.Datum;
        match.Kolo = request.Kolo;
        match.Sezona = request.Sezona.Trim();
        match.GolDomacin = request.GolDomacin;
        match.GolGost = request.GolGost;
        match.Status = request.Status.Trim();

        var updated = await _matchesRepository.UpdateAsync(id, match, cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return Ok(match);
    }

    [Authorize(Roles = "administrator")]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMatchAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var deleted = await _matchesRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private static bool IsValidMatchRequest(
        string domacinId,
        string gostId,
        int kolo,
        string sezona,
        string status,
        int? golDomacin,
        int? golGost)
    {
        return !string.IsNullOrWhiteSpace(domacinId)
            && !string.IsNullOrWhiteSpace(gostId)
            && kolo > 0
            && !string.IsNullOrWhiteSpace(sezona)
            && !string.IsNullOrWhiteSpace(status)
            && (!golDomacin.HasValue || golDomacin.Value >= 0)
            && (!golGost.HasValue || golGost.Value >= 0);
    }
}
