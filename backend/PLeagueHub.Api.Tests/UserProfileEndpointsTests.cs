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
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Tests;

public sealed class UserProfileEndpointsTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";
    private const string UserId = "665000000000000000000901";
    private const string ArsenalId = "665000000000000000000001";
    private const string LiverpoolId = "665000000000000000000002";

    [Fact]
    public async Task GetMe_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_ReturnsCurrentUserProfile()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory);

        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me");

        Assert.NotNull(profile);
        Assert.Equal(UserId, profile.UserId);
        Assert.Equal("fan", profile.Username);
        Assert.Contains(ArsenalId, profile.FavoritniTimovi);
    }

    [Fact]
    public async Task UpdateFavoriteTeams_UpdatesCurrentUserFavorites()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PutAsJsonAsync("/api/users/me/favorite-teams", new UpdateFavoriteTeamsRequest
        {
            TeamIds = [LiverpoolId, ArsenalId, LiverpoolId]
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal([LiverpoolId, ArsenalId], profile.FavoritniTimovi);
    }

    [Fact]
    public async Task UpdateFavoriteTeams_ReturnsBadRequest_WhenTeamDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PutAsJsonAsync("/api/users/me/favorite-teams", new UpdateFavoriteTeamsRequest
        {
            TeamIds = ["missing-team"]
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken(UserId, "registrovani"));

        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<User>>();
                    services.RemoveAll<IRepository<Team>>();

                    services.AddSingleton<IRepository<User>>(new FakeRepository<User>(
                    [
                        new User
                        {
                            Id = UserId,
                            Username = "fan",
                            Email = "fan@example.com",
                            PasswordHash = "hash",
                            Uloga = "registrovani",
                            Aktivan = true,
                            DatumReg = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
                            FavoritniTimovi = [ArsenalId]
                        }
                    ]));

                    services.AddSingleton<IRepository<Team>>(new FakeRepository<Team>(
                    [
                        new Team
                        {
                            Id = ArsenalId,
                            Naziv = "Arsenal",
                            Skracenica = "ARS",
                            Stadion = "Emirates Stadium",
                            Osnovan = 1886,
                            LogoUrl = "https://resources.premierleague.com/premierleague/badges/t3.svg",
                            Bodovi = 18,
                            Pozicija = 1
                        },
                        new Team
                        {
                            Id = LiverpoolId,
                            Naziv = "Liverpool",
                            Skracenica = "LIV",
                            Stadion = "Anfield",
                            Osnovan = 1892,
                            LogoUrl = "https://resources.premierleague.com/premierleague/badges/t14.svg",
                            Bodovi = 17,
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
                new Claim(ClaimTypes.Name, "fan"),
                new Claim(ClaimTypes.Email, "fan@example.com"),
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
            document.Id ??= Guid.NewGuid().ToString("N");
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
            var deletedCount = _documents.RemoveAll(document => document.Id == id);
            return Task.FromResult(deletedCount > 0);
        }
    }
}
