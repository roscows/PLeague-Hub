using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;

namespace PLeagueHub.Api.Tests;

public sealed class TeamWriteEndpointsTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";

    [Fact]
    public async Task CreateTeam_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/teams", CreateTeamRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_ReturnsForbidden_WhenUserIsNotAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "moderator");

        var response = await client.PostAsJsonAsync("/api/teams", CreateTeamRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_CreatesTeam_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PostAsJsonAsync("/api/teams", CreateTeamRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var teams = await client.GetFromJsonAsync<List<Team>>("/api/teams");
        Assert.NotNull(teams);
        Assert.Contains(teams, team =>
            team.Naziv == "Liverpool"
            && team.Skracenica == "LIV"
            && team.Pozicija == 1);
    }

    [Fact]
    public async Task UpdateTeam_UpdatesTeam_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsJsonAsync("/api/teams/team-1", UpdateTeamRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var team = await response.Content.ReadFromJsonAsync<Team>();
        Assert.NotNull(team);
        Assert.Equal("Arsenal FC", team.Naziv);
        Assert.Equal(42, team.Bodovi);
        Assert.Equal(2, team.Pozicija);
    }

    [Fact]
    public async Task UpdateTeam_ReturnsNotFound_WhenTeamDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsJsonAsync("/api/teams/missing", UpdateTeamRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTeam_RemovesTeam_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.DeleteAsync("/api/teams/team-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var teams = await client.GetFromJsonAsync<List<Team>>("/api/teams");
        Assert.NotNull(teams);
        Assert.DoesNotContain(teams, team => team.Id == "team-1");
    }

    private static CreateTeamRequest CreateTeamRequest()
    {
        return new CreateTeamRequest
        {
            Naziv = "Liverpool",
            Skracenica = "LIV",
            Stadion = "Anfield",
            Osnovan = 1892,
            LogoUrl = "https://example.com/liverpool.png",
            Bodovi = 45,
            Pozicija = 1
        };
    }

    private static UpdateTeamRequest UpdateTeamRequest()
    {
        return new UpdateTeamRequest
        {
            Naziv = "Arsenal FC",
            Skracenica = "ARS",
            Stadion = "Emirates Stadium",
            Osnovan = 1886,
            LogoUrl = "https://example.com/arsenal.png",
            Bodovi = 42,
            Pozicija = 2
        };
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("665000000000000000000901", role));

        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<Team>>();
                    services.AddSingleton<IRepository<Team>>(new FakeRepository<Team>(
                    [
                        new Team
                        {
                            Id = "team-1",
                            Naziv = "Arsenal",
                            Skracenica = "ARS",
                            Stadion = "Highbury",
                            Osnovan = 1886,
                            LogoUrl = "https://example.com/arsenal-old.png",
                            Bodovi = 36,
                            Pozicija = 3
                        }
                    ]));
                });
            });
    }

    private static string CreateToken(string userId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "PLeagueHub",
            audience: "PLeagueHub",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Email, "admin@example.com"),
                new Claim(ClaimTypes.Role, role)
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FakeRepository<TDocument> : IRepository<TDocument>
        where TDocument : BaseDocument
    {
        private readonly List<TDocument> _documents;

        public FakeRepository(IEnumerable<TDocument> documents)
        {
            _documents = documents.ToList();
        }

        public Task<IReadOnlyCollection<TDocument>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<TDocument>>(_documents);
        }

        public Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_documents.FirstOrDefault(document => document.Id == id));
        }

        public Task<TDocument?> FindOneAsync(
            Expression<Func<TDocument, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_documents.AsQueryable().FirstOrDefault(predicate));
        }

        public Task<bool> ExistsAsync(
            Expression<Func<TDocument, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_documents.AsQueryable().Any(predicate));
        }

        public Task<TDocument> CreateAsync(TDocument document, CancellationToken cancellationToken = default)
        {
            document.Id = $"team-{_documents.Count + 1}";
            _documents.Add(document);
            return Task.FromResult(document);
        }

        public Task<bool> UpdateAsync(string id, TDocument document, CancellationToken cancellationToken = default)
        {
            var index = _documents.FindIndex(existing => existing.Id == id);

            if (index < 0)
            {
                return Task.FromResult(false);
            }

            document.Id = id;
            _documents[index] = document;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            var removed = _documents.RemoveAll(document => document.Id == id) > 0;
            return Task.FromResult(removed);
        }
    }
}
