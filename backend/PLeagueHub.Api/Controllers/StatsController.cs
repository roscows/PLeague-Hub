using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;

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
        [FromQuery] string? teamId,
        [FromQuery] string? position,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var players = await _playersRepository.GetAllAsync(cancellationToken);
        var filteredPlayers = players.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(teamId))
        {
            filteredPlayers = filteredPlayers.Where(player =>
                string.Equals(player.TeamId, teamId.Trim(), StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(position))
        {
            filteredPlayers = filteredPlayers.Where(player =>
                string.Equals(player.Pozicija, position.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filteredPlayers = filteredPlayers.Where(player =>
                player.Ime.Contains(term, StringComparison.OrdinalIgnoreCase)
                || player.Prezime.Contains(term, StringComparison.OrdinalIgnoreCase)
                || $"{player.Ime} {player.Prezime}".Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(filteredPlayers
            .OrderByDescending(player => player.Golovi)
            .ThenByDescending(player => player.Asistencije)
            .ThenBy(player => player.Prezime)
            .ToList());
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

    [Authorize(Roles = "administrator")]
    [HttpPost("players")]
    [ProducesResponseType(typeof(Player), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Player>> CreatePlayerAsync(
        CreatePlayerRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidPlayerRequest(
            request.TeamId,
            request.Ime,
            request.Prezime,
            request.Pozicija,
            request.Nacionalnost,
            request.Golovi,
            request.Asistencije,
            request.Ocena))
        {
            return BadRequest(new { message = "TeamId, ime, prezime, pozicija i nacionalnost su obavezni." });
        }

        var team = await _teamsRepository.GetByIdAsync(request.TeamId.Trim(), cancellationToken);

        if (team is null)
        {
            return BadRequest(new { message = $"Team '{request.TeamId}' does not exist." });
        }

        var player = new Player
        {
            TeamId = request.TeamId.Trim(),
            Ime = request.Ime.Trim(),
            Prezime = request.Prezime.Trim(),
            Pozicija = request.Pozicija.Trim(),
            Nacionalnost = request.Nacionalnost.Trim(),
            Golovi = request.Golovi,
            Asistencije = request.Asistencije,
            Ocena = request.Ocena
        };

        var createdPlayer = await _playersRepository.CreateAsync(player, cancellationToken);

        return Created($"/api/players/{createdPlayer.Id}", createdPlayer);
    }

    [Authorize(Roles = "administrator")]
    [HttpPut("players/{id}")]
    [ProducesResponseType(typeof(Player), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Player>> UpdatePlayerAsync(
        string id,
        UpdatePlayerRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidPlayerRequest(
            request.TeamId,
            request.Ime,
            request.Prezime,
            request.Pozicija,
            request.Nacionalnost,
            request.Golovi,
            request.Asistencije,
            request.Ocena))
        {
            return BadRequest(new { message = "TeamId, ime, prezime, pozicija i nacionalnost su obavezni." });
        }

        var existingPlayer = await _playersRepository.GetByIdAsync(id, cancellationToken);

        if (existingPlayer is null)
        {
            return NotFound();
        }

        var team = await _teamsRepository.GetByIdAsync(request.TeamId.Trim(), cancellationToken);

        if (team is null)
        {
            return BadRequest(new { message = $"Team '{request.TeamId}' does not exist." });
        }

        existingPlayer.TeamId = request.TeamId.Trim();
        existingPlayer.Ime = request.Ime.Trim();
        existingPlayer.Prezime = request.Prezime.Trim();
        existingPlayer.Pozicija = request.Pozicija.Trim();
        existingPlayer.Nacionalnost = request.Nacionalnost.Trim();
        existingPlayer.Golovi = request.Golovi;
        existingPlayer.Asistencije = request.Asistencije;
        existingPlayer.Ocena = request.Ocena;

        var updated = await _playersRepository.UpdateAsync(id, existingPlayer, cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return Ok(existingPlayer);
    }

    [Authorize(Roles = "administrator")]
    [HttpDelete("players/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePlayerAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var deleted = await _playersRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("teams")]
    [ProducesResponseType(typeof(IReadOnlyCollection<Team>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Team>>> GetTeamsAsync(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var teams = await _teamsRepository.GetAllAsync(cancellationToken);
        var filteredTeams = teams.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filteredTeams = filteredTeams.Where(team =>
                team.Naziv.Contains(term, StringComparison.OrdinalIgnoreCase)
                || team.Skracenica.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(filteredTeams.OrderBy(team => team.Pozicija).ToList());
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

    [Authorize(Roles = "administrator")]
    [HttpPost("teams")]
    [ProducesResponseType(typeof(Team), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Team>> CreateTeamAsync(
        CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidTeamRequest(
            request.Naziv,
            request.Skracenica,
            request.Stadion,
            request.Osnovan,
            request.Bodovi,
            request.Pozicija))
        {
            return BadRequest(new { message = "Naziv, skracenica, stadion, osnovan i pozicija su obavezni." });
        }

        var team = new Team
        {
            Naziv = request.Naziv.Trim(),
            Skracenica = request.Skracenica.Trim(),
            Stadion = request.Stadion.Trim(),
            Osnovan = request.Osnovan,
            LogoUrl = request.LogoUrl.Trim(),
            Bodovi = request.Bodovi,
            Pozicija = request.Pozicija
        };

        var createdTeam = await _teamsRepository.CreateAsync(team, cancellationToken);

        return Created($"/api/teams/{createdTeam.Id}", createdTeam);
    }

    [Authorize(Roles = "administrator")]
    [HttpPut("teams/{id}")]
    [ProducesResponseType(typeof(Team), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Team>> UpdateTeamAsync(
        string id,
        UpdateTeamRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValidTeamRequest(
            request.Naziv,
            request.Skracenica,
            request.Stadion,
            request.Osnovan,
            request.Bodovi,
            request.Pozicija))
        {
            return BadRequest(new { message = "Naziv, skracenica, stadion, osnovan i pozicija su obavezni." });
        }

        var existingTeam = await _teamsRepository.GetByIdAsync(id, cancellationToken);

        if (existingTeam is null)
        {
            return NotFound();
        }

        existingTeam.Naziv = request.Naziv.Trim();
        existingTeam.Skracenica = request.Skracenica.Trim();
        existingTeam.Stadion = request.Stadion.Trim();
        existingTeam.Osnovan = request.Osnovan;
        existingTeam.LogoUrl = request.LogoUrl.Trim();
        existingTeam.Bodovi = request.Bodovi;
        existingTeam.Pozicija = request.Pozicija;

        var updated = await _teamsRepository.UpdateAsync(id, existingTeam, cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return Ok(existingTeam);
    }

    [Authorize(Roles = "administrator")]
    [HttpDelete("teams/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTeamAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var deleted = await _teamsRepository.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private static bool IsValidTeamRequest(
        string naziv,
        string skracenica,
        string stadion,
        int osnovan,
        int bodovi,
        int pozicija)
    {
        return !string.IsNullOrWhiteSpace(naziv)
            && !string.IsNullOrWhiteSpace(skracenica)
            && !string.IsNullOrWhiteSpace(stadion)
            && osnovan > 0
            && bodovi >= 0
            && pozicija > 0;
    }

    private static bool IsValidPlayerRequest(
        string teamId,
        string ime,
        string prezime,
        string pozicija,
        string nacionalnost,
        int golovi,
        int asistencije,
        double ocena)
    {
        return !string.IsNullOrWhiteSpace(teamId)
            && !string.IsNullOrWhiteSpace(ime)
            && !string.IsNullOrWhiteSpace(prezime)
            && !string.IsNullOrWhiteSpace(pozicija)
            && !string.IsNullOrWhiteSpace(nacionalnost)
            && golovi >= 0
            && asistencije >= 0
            && ocena is >= 0 and <= 10;
    }
}
