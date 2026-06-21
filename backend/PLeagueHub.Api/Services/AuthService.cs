using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public sealed class AuthService
{
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IPasswordService _passwordService;
    private readonly IRepository<User> _usersRepository;
    private readonly IModerationService _moderationService;

    public AuthService(
        IRepository<User> usersRepository,
        IPasswordService passwordService,
        IJwtTokenService jwtTokenService,
        IModerationService moderationService)
    {
        _usersRepository = usersRepository;
        _passwordService = passwordService;
        _jwtTokenService = jwtTokenService;
        _moderationService = moderationService;
    }

    public async Task<AuthServiceResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(email)
            || request.Password.Length < 8)
        {
            return AuthServiceResult.BadRequest("Username, email and password with at least 8 characters are required.");
        }

        var emailExists = await _usersRepository.ExistsAsync(
            user => user.Email == email,
            cancellationToken);

        if (emailExists)
        {
            return AuthServiceResult.Conflict("Email is already registered.");
        }

        var usernameExists = await _usersRepository.ExistsAsync(
            user => user.Username == username,
            cancellationToken);

        if (usernameExists)
        {
            return AuthServiceResult.Conflict("Username is already registered.");
        }

        var user = new User
        {
            Username = username,
            Email = email,
            Uloga = "registrovani",
            Aktivan = true,
            DatumReg = DateTime.UtcNow,
            FavoritniTimovi = []
        };
        user.PasswordHash = _passwordService.HashPassword(user, request.Password);

        var createdUser = await _usersRepository.CreateAsync(user, cancellationToken);
        return AuthServiceResult.Created(CreateAuthResponse(createdUser));
    }

    public async Task<AuthServiceResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var emailOrUsername = request.EmailOrUsername.Trim();
        var normalizedEmail = emailOrUsername.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(emailOrUsername) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthServiceResult.BadRequest("Email/username and password are required.");
        }

        var user = await _usersRepository.FindOneAsync(
            candidate => candidate.Email == normalizedEmail || candidate.Username == emailOrUsername,
            cancellationToken);

        if (user is null || !user.Aktivan || !_passwordService.VerifyPassword(user, request.Password))
        {
            return AuthServiceResult.Unauthorized("Invalid credentials.");
        }

        var access = await _moderationService.CheckLoginAsync(user, cancellationToken);
        if (!access.Allowed)
        {
            return AuthServiceResult.Unauthorized(access.Message ?? "Nalog je suspendovan.", access.State);
        }

        return AuthServiceResult.Ok(CreateAuthResponse(user));
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var token = _jwtTokenService.CreateToken(user);

        return new AuthResponse
        {
            UserId = user.Id ?? string.Empty,
            Username = user.Username,
            Email = user.Email,
            Uloga = user.Uloga,
            Token = token.Token,
            ExpiresAt = token.ExpiresAt
        };
    }
}
