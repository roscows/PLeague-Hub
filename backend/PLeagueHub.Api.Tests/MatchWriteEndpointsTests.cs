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

public sealed class MatchWriteEndpointsTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";

    [Fact]
    public async Task CreateMatch_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/matches", CreateMatchRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_ReturnsForbidden_WhenUserIsNotAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "moderator");

        var response = await client.PostAsJsonAsync("/api/matches", CreateMatchRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_CreatesMatch_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PostAsJsonAsync("/api/matches", CreateMatchRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var matches = await client.GetFromJsonAsync<List<Match>>("/api/matches");
        Assert.NotNull(matches);
        Assert.Contains(matches, match =>
            match.DomacinId == "team-1"
            && match.GostId == "team-2"
            && match.Kolo == 2
            && match.Status == "zakazana");
    }

    [Fact]
    public async Task CreateMatch_ReturnsBadRequest_WhenTeamDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");
        var request = CreateMatchRequest();
        request.GostId = "missing-team";

        var response = await client.PostAsJsonAsync("/api/matches", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateMatch_ReturnsBadRequest_WhenTeamsAreSame()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");
        var request = CreateMatchRequest();
        request.GostId = request.DomacinId;

        var response = await client.PostAsJsonAsync("/api/matches", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMatch_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/matches/match-1", CreateRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMatch_ReturnsForbidden_WhenUserIsNotAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "moderator");

        var response = await client.PutAsJsonAsync("/api/matches/match-1", CreateRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMatch_UpdatesScoreAndStatus_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsJsonAsync("/api/matches/match-1", CreateRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var match = await response.Content.ReadFromJsonAsync<Match>();
        Assert.NotNull(match);
        Assert.Equal(3, match.GolDomacin);
        Assert.Equal(1, match.GolGost);
        Assert.Equal("zavrsena", match.Status);
    }

    [Fact]
    public async Task UpdateMatch_ReturnsNotFound_WhenMatchDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsJsonAsync("/api/matches/missing", CreateRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMatch_RemovesMatch_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.DeleteAsync("/api/matches/match-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var matches = await client.GetFromJsonAsync<List<Match>>("/api/matches");
        Assert.NotNull(matches);
        Assert.DoesNotContain(matches, match => match.Id == "match-1");
    }

    [Fact]
    public async Task DeleteMatch_ReturnsNotFound_WhenMatchDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.DeleteAsync("/api/matches/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CreateMatchRequest CreateMatchRequest()
    {
        return new CreateMatchRequest
        {
            DomacinId = "team-1",
            GostId = "team-2",
            Datum = new DateTime(2026, 8, 22, 16, 0, 0, DateTimeKind.Utc),
            Kolo = 2,
            Sezona = "2026/27",
            GolDomacin = null,
            GolGost = null,
            Status = "zakazana"
        };
    }

    private static UpdateMatchRequest CreateRequest()
    {
        return new UpdateMatchRequest
        {
            Datum = new DateTime(2026, 8, 15, 16, 0, 0, DateTimeKind.Utc),
            Kolo = 1,
            Sezona = "2026/27",
            GolDomacin = 3,
            GolGost = 1,
            Status = "zavrsena"
        };
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("665000000000000000000801", role));

        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<Match>>();
                    services.RemoveAll<IRepository<Team>>();
                    services.AddSingleton<IRepository<Match>>(new FakeRepository<Match>(
                    [
                        new Match
                        {
                            Id = "match-1",
                            DomacinId = "team-1",
                            GostId = "team-2",
                            Datum = new DateTime(2026, 8, 15, 16, 0, 0, DateTimeKind.Utc),
                            Kolo = 1,
                            Sezona = "2026/27",
                            GolDomacin = null,
                            GolGost = null,
                            Status = "zakazana"
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
                        },
                        new Team
                        {
                            Id = "team-2",
                            Naziv = "Liverpool",
                            Skracenica = "LIV",
                            Stadion = "Anfield",
                            Osnovan = 1892,
                            LogoUrl = "https://example.com/liverpool.png",
                            Bodovi = 40,
                            Pozicija = 2
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
            document.Id = $"match-{_documents.Count + 1}";
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
