using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class UsersController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IRepository<User> _usersRepository;

    public UsersController(AuthService authService, IRepository<User> usersRepository)
    {
        _authService = authService;
        _usersRepository = usersRepository;
    }

    [HttpPost("auth/register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);

        return result.StatusCode switch
        {
            StatusCodes.Status201Created => Created(
                $"/api/users/{result.Response!.UserId}",
                result.Response),
            StatusCodes.Status409Conflict => Conflict(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }

    [HttpPost("auth/login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);

        return result.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(result.Response),
            StatusCodes.Status401Unauthorized => Unauthorized(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }

    [HttpGet("users/{id}")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetUserByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var user = await _usersRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(new UserProfileResponse
        {
            UserId = user.Id ?? string.Empty,
            Username = user.Username,
            Email = user.Email,
            Uloga = user.Uloga,
            Aktivan = user.Aktivan,
            DatumReg = user.DatumReg,
            FavoritniTimovi = user.FavoritniTimovi
        });
    }

    [Authorize]
    [HttpGet("users/me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> GetCurrentUserAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _usersRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        return Ok(ToProfileResponse(user));
    }

    [Authorize]
    [HttpPut("users/me/favorite-teams")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileResponse>> UpdateFavoriteTeamsAsync(
        UpdateFavoriteTeamsRequest request,
        [FromServices] IRepository<Team> teamsRepository,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await _usersRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        var teamIds = request.TeamIds
            .Where(teamId => !string.IsNullOrWhiteSpace(teamId))
            .Select(teamId => teamId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var teamId in teamIds)
        {
            var team = await teamsRepository.GetByIdAsync(teamId, cancellationToken);

            if (team is null)
            {
                return BadRequest(new { message = $"Team '{teamId}' does not exist." });
            }
        }

        user.FavoritniTimovi = teamIds;
        var updated = await _usersRepository.UpdateAsync(userId, user, cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return Ok(ToProfileResponse(user));
    }

    private static UserProfileResponse ToProfileResponse(User user)
    {
        return new UserProfileResponse
        {
            UserId = user.Id ?? string.Empty,
            Username = user.Username,
            Email = user.Email,
            Uloga = user.Uloga,
            Aktivan = user.Aktivan,
            DatumReg = user.DatumReg,
            FavoritniTimovi = user.FavoritniTimovi
        };
    }
}
