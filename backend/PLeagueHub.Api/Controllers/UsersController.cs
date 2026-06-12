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
}
