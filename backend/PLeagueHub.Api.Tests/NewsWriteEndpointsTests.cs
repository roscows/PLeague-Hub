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

public sealed class NewsWriteEndpointsTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";

    [Fact]
    public async Task CreateNews_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/news", CreateRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateNews_ReturnsForbidden_WhenUserIsNotAdministrator()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("665000000000000000000601", "registrovani"));

        var response = await client.PostAsJsonAsync("/api/news", CreateRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateNews_CreatesVisibleNews_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("665000000000000000000601", "administrator"));

        var response = await client.PostAsJsonAsync("/api/news", CreateRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var news = await client.GetFromJsonAsync<List<Post>>("/api/news");
        Assert.NotNull(news);
        Assert.Contains(news, post => post.Naslov == "Admin vest" && post.Tip == "vest" && !post.Obrisan);
    }

    [Fact]
    public async Task DeleteNews_SoftDeletesNews_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("665000000000000000000601", "administrator"));

        var response = await client.DeleteAsync("/api/news/news-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var news = await client.GetFromJsonAsync<List<Post>>("/api/news");
        Assert.NotNull(news);
        Assert.DoesNotContain(news, post => post.Id == "news-1");
    }

    [Fact]
    public async Task DeleteNews_ReturnsNotFound_WhenPostIsNotNews()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("665000000000000000000601", "administrator"));

        var response = await client.DeleteAsync("/api/news/discussion-1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CreateNewsRequest CreateRequest()
    {
        return new CreateNewsRequest
        {
            Naslov = "Admin vest",
            Sadrzaj = "Administrator objavljuje novu vest."
        };
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<Post>>();
                    services.AddSingleton<IRepository<Post>>(new FakeRepository<Post>(
                    [
                        new Post
                        {
                            Id = "news-1",
                            AutorId = "665000000000000000000501",
                            Naslov = "Postojeca vest",
                            Sadrzaj = "Vest koja postoji pre testa.",
                            Tip = "vest",
                            DatumKreiranja = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
                            Obrisan = false
                        },
                        new Post
                        {
                            Id = "discussion-1",
                            AutorId = "665000000000000000000501",
                            Naslov = "Forum tema",
                            Sadrzaj = "Diskusija nije vest.",
                            Tip = "diskusija",
                            DatumKreiranja = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc),
                            Obrisan = false
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
