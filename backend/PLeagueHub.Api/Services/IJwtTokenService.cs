using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}
