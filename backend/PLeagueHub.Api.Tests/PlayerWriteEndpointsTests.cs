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

public sealed class PlayerWriteEndpointsTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";

    [Fact]
    public async Task CreatePlayer_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/players", CreatePlayerRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreatePlayer_ReturnsForbidden_WhenUserIsNotAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "moderator");

        var response = await client.PostAsJsonAsync("/api/players", CreatePlayerRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatePlayer_CreatesPlayer_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PostAsJsonAsync("/api/players", CreatePlayerRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var players = await client.GetFromJsonAsync<List<Player>>("/api/players");
        Assert.NotNull(players);
        Assert.Contains(players, player =>
            player.Ime == "Bukayo"
            && player.Prezime == "Saka"
            && player.TeamId == "team-1");
    }

    [Fact]
    public async Task CreatePlayer_ReturnsBadRequest_WhenTeamDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");
        var request = CreatePlayerRequest();
        request.TeamId = "missing-team";

        var response = await client.PostAsJsonAsync("/api/players", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePlayer_UpdatesPlayer_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsJsonAsync("/api/players/player-1", UpdatePlayerRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var player = await response.Content.ReadFromJsonAsync<Player>();
        Assert.NotNull(player);
        Assert.Equal("Martin", player.Ime);
        Assert.Equal("Odegaard", player.Prezime);
        Assert.Equal(8, player.Asistencije);
    }

    [Fact]
    public async Task UpdatePlayer_ReturnsNotFound_WhenPlayerDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsJsonAsync("/api/players/missing", UpdatePlayerRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletePlayer_RemovesPlayer_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.DeleteAsync("/api/players/player-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var players = await client.GetFromJsonAsync<List<Player>>("/api/players");
        Assert.NotNull(players);
        Assert.DoesNotContain(players, player => player.Id == "player-1");
    }

    private static CreatePlayerRequest CreatePlayerRequest()
    {
        return new CreatePlayerRequest
        {
            TeamId = "team-1",
            Ime = "Bukayo",
            Prezime = "Saka",
            Pozicija = "RW",
            Nacionalnost = "England",
            Golovi = 12,
            Asistencije = 9,
            Ocena = 8.4
        };
    }

    private static UpdatePlayerRequest UpdatePlayerRequest()
    {
        return new UpdatePlayerRequest
        {
            TeamId = "team-1",
            Ime = "Martin",
            Prezime = "Odegaard",
            Pozicija = "CM",
            Nacionalnost = "Norway",
            Golovi = 7,
            Asistencije = 8,
            Ocena = 8.1
        };
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("665000000000000000001001", role));

        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<Player>>();
                    services.RemoveAll<IRepository<Team>>();
                    services.AddSingleton<IRepository<Player>>(new FakeRepository<Player>(
                    [
                        new Player
                        {
                            Id = "player-1",
                            TeamId = "team-1",
                            Ime = "Gabriel",
                            Prezime = "Martinelli",
                            Pozicija = "LW",
                            Nacionalnost = "Brazil",
                            Golovi = 6,
                            Asistencije = 5,
                            Ocena = 7.7
                        }
                    ]));
                    services.AddSingleton<IRepository<Team>>(new FakeRepository<Team>(
                    [
                        new Team
                        {
                            Id = "team-1",
                            Naziv = "Arsenal",
                            Skracenica = "ARS",
                            Stadion = "Emirates Stadium",
                            Osnovan = 1886,
                            LogoUrl = "https://example.com/arsenal.png",
                            Bodovi = 42,
                            Pozicija = 1
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
            document.Id = $"player-{_documents.Count + 1}";
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
