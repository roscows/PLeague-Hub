using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class IntegrationEndpointsTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";

    [Fact]
    public async Task SyncTeams_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/integrations/football/sync/teams?seasonId=76986",
            null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncTeams_ReturnsForbidden_WhenRoleIsModerator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "moderator");

        var response = await client.PostAsync(
            "/api/integrations/football/sync/teams?seasonId=76986",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SyncTeams_ReturnsBadRequest_WhenSeasonIdIsNotPositive()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PostAsync(
            "/api/integrations/football/sync/teams?seasonId=0",
            null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SyncTeams_ReturnsSummary_WhenAdministratorRunsSync()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PostAsync(
            "/api/integrations/football/sync/teams?seasonId=76986",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<TeamSyncResponse>();
        Assert.NotNull(summary);
        Assert.Equal(17, summary.TournamentId);
        Assert.Equal(76986, summary.SeasonId);
        Assert.Equal(1, summary.Created);
        Assert.Equal(0, summary.Updated);
        Assert.Equal(0, summary.Skipped);
    }

    [Fact]
    public async Task SyncTeams_ReturnsBadGateway_WhenProviderFails()
    {
        using var factory = CreateFactory(providerFailure: true);
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PostAsync(
            "/api/integrations/football/sync/teams?seasonId=76986",
            null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task SyncTeamLogos_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/integrations/football/sync/team-logos",
            null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SyncTeamLogos_ReturnsForbidden_WhenRoleIsModerator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "moderator");

        var response = await client.PostAsync(
            "/api/integrations/football/sync/team-logos",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SyncTeamLogos_ReturnsSummary_WhenAdministratorRunsSync()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PostAsync(
            "/api/integrations/football/sync/team-logos",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<TeamLogoSyncResponse>();
        Assert.NotNull(summary);
        Assert.Equal(0, summary.Downloaded);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task SyncTeamLogos_ReturnsBadGateway_WhenTeamsCannotBeLoaded()
    {
        using var factory = CreateFactory(teamRepositoryFailure: true);
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PostAsync(
            "/api/integrations/football/sync/team-logos",
            null);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        bool providerFailure = false,
        bool teamRepositoryFailure = false)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IFootballProvider>();
                    services.RemoveAll<IRepository<Team>>();
                    services.AddSingleton<IFootballProvider>(
                        new FakeFootballProvider(providerFailure));
                    services.AddSingleton<IRepository<Team>>(
                        new FakeTeamRepository(teamRepositoryFailure));
                });
            });
    }

    private static HttpClient CreateAuthenticatedClient(
        WebApplicationFactory<Program> factory,
        string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(role));
        return client;
    }

    private static string CreateToken(string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "PLeagueHub",
            audience: "PLeagueHub",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, "665000000000000000000901"),
                new Claim(ClaimTypes.Name, "integration-admin"),
                new Claim(ClaimTypes.Email, "integration@example.com"),
                new Claim(ClaimTypes.Role, role)
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FakeFootballProvider(bool shouldFail) : IFootballProvider
    {
        public Task<FootballTeamLogo> GetTeamLogoAsync(
            int providerId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyCollection<FootballTeamStanding>> GetTeamStandingsAsync(
            int tournamentId,
            int seasonId,
            CancellationToken cancellationToken = default)
        {
            if (shouldFail)
            {
                throw new HttpRequestException("Provider unavailable.");
            }

            return Task.FromResult<IReadOnlyCollection<FootballTeamStanding>>(
            [
                new FootballTeamStanding(42, "Arsenal", "ARS", 1, 85)
            ]);
        }

        public Task<JsonDocument> SearchAsync(
            string term,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeTeamRepository(bool shouldFail = false) : IRepository<Team>
    {
        private readonly List<Team> _teams = [];

        public Task<IReadOnlyCollection<Team>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            if (shouldFail)
            {
                throw new InvalidOperationException("Repository unavailable.");
            }

            return Task.FromResult<IReadOnlyCollection<Team>>(_teams);
        }

        public Task<Team?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_teams.FirstOrDefault(team => team.Id == id));
        }

        public Task<Team?> FindOneAsync(
            Expression<Func<Team, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_teams.AsQueryable().FirstOrDefault(predicate));
        }

        public Task<bool> ExistsAsync(
            Expression<Func<Team, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_teams.AsQueryable().Any(predicate));
        }

        public Task<Team> CreateAsync(Team document, CancellationToken cancellationToken = default)
        {
            document.Id = "665000000000000000000001";
            _teams.Add(document);
            return Task.FromResult(document);
        }

        public Task<bool> UpdateAsync(
            string id,
            Team document,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_teams.Any(team => team.Id == id));
        }

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
