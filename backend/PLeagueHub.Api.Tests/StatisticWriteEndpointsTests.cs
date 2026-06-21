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

public sealed class StatisticWriteEndpointsTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";

    [Fact]
    public async Task CreateStatistic_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/statistics", CreateStatisticRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateStatistic_ReturnsForbidden_WhenUserIsNotAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "moderator");

        var response = await client.PostAsJsonAsync("/api/statistics", CreateStatisticRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStatistic_CreatesStatistic_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PostAsJsonAsync("/api/statistics", CreateStatisticRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var statistics = await client.GetFromJsonAsync<List<Statistic>>("/api/statistics?matchId=match-1");
        Assert.NotNull(statistics);
        Assert.Contains(statistics, statistic =>
            statistic.PlayerId == "player-2"
            && statistic.Golovi == 2
            && statistic.Ocena == 9.1);
    }

    [Fact]
    public async Task CreateStatistic_ReturnsBadRequest_WhenMatchDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");
        var request = CreateStatisticRequest();
        request.MatchId = "missing-match";

        var response = await client.PostAsJsonAsync("/api/statistics", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateStatistic_ReturnsBadRequest_WhenPlayerDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");
        var request = CreateStatisticRequest();
        request.PlayerId = "missing-player";

        var response = await client.PostAsJsonAsync("/api/statistics", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatistic_UpdatesStatistic_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsJsonAsync("/api/statistics/stat-1", UpdateStatisticRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var statistic = await response.Content.ReadFromJsonAsync<Statistic>();
        Assert.NotNull(statistic);
        Assert.Equal(1, statistic.Golovi);
        Assert.Equal(2, statistic.Asistencije);
        Assert.Equal(8.8, statistic.Ocena);
    }

    [Fact]
    public async Task UpdateStatistic_ReturnsNotFound_WhenStatisticDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsJsonAsync("/api/statistics/missing", UpdateStatisticRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteStatistic_RemovesStatistic_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.DeleteAsync("/api/statistics/stat-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var statistics = await client.GetFromJsonAsync<List<Statistic>>("/api/statistics");
        Assert.NotNull(statistics);
        Assert.DoesNotContain(statistics, statistic => statistic.Id == "stat-1");
    }

    private static CreateStatisticRequest CreateStatisticRequest()
    {
        return new CreateStatisticRequest
        {
            MatchId = "match-1",
            PlayerId = "player-2",
            Golovi = 2,
            Asistencije = 0,
            Kartoni = 0,
            MinutiIgre = 90,
            Ocena = 9.1
        };
    }

    private static UpdateStatisticRequest UpdateStatisticRequest()
    {
        return new UpdateStatisticRequest
        {
            MatchId = "match-1",
            PlayerId = "player-1",
            Golovi = 1,
            Asistencije = 2,
            Kartoni = 0,
            MinutiIgre = 90,
            Ocena = 8.8
        };
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("665000000000000000001101", role));

        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<Statistic>>();
                    services.RemoveAll<IRepository<Match>>();
                    services.RemoveAll<IRepository<Player>>();

                    services.AddSingleton<IRepository<Statistic>>(new FakeRepository<Statistic>(
                    [
                        new Statistic
                        {
                            Id = "stat-1",
                            MatchId = "match-1",
                            PlayerId = "player-1",
                            Golovi = 0,
                            Asistencije = 1,
                            Kartoni = 0,
                            MinutiIgre = 90,
                            Ocena = 7.9
                        }
                    ]));
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
                            GolDomacin = 2,
                            GolGost = 1,
                            Status = "zavrsena"
                        }
                    ]));
                    services.AddSingleton<IRepository<Player>>(new FakeRepository<Player>(
                    [
                        new Player
                        {
                            Id = "player-1",
                            TeamId = "team-1",
                            Ime = "Martin",
                            Prezime = "Odegaard",
                            Pozicija = "CM",
                            Nacionalnost = "Norway",
                            Golovi = 7,
                            Asistencije = 8,
                            Ocena = 8.1
                        },
                        new Player
                        {
                            Id = "player-2",
                            TeamId = "team-2",
                            Ime = "Mohamed",
                            Prezime = "Salah",
                            Pozicija = "RW",
                            Nacionalnost = "Egypt",
                            Golovi = 16,
                            Asistencije = 9,
                            Ocena = 8.7
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
            document.Id = $"stat-{_documents.Count + 1}";
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
