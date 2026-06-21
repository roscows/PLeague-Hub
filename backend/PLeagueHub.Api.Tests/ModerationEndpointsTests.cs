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
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Tests;

public sealed class ModerationEndpointsTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";

    [Fact]
    public async Task DeletePost_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/moderation/posts/discussion-1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeletePost_ReturnsForbidden_WhenUserIsNotModeratorOrAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "registrovani");

        var response = await client.DeleteAsync("/api/moderation/posts/discussion-1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeletePost_SoftDeletesPost_WhenUserIsModerator()
    {
        var postsRepository = CreatePostsRepository();
        using var factory = CreateFactory(postsRepository);
        var client = CreateAuthenticatedClient(factory, "moderator");

        var response = await client.DeleteAsync("/api/moderation/posts/discussion-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var deletedPost = await postsRepository.GetByIdAsync("discussion-1");
        Assert.NotNull(deletedPost);
        Assert.True(deletedPost.Obrisan);
    }

    [Fact]
    public async Task SuspendUser_ReturnsForbidden_WhenUserIsNotModeratorOrAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "registrovani");

        var response = await client.PutAsync("/api/moderation/users/user-1/suspend", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuspendUser_DeactivatesUser_WhenUserIsAdministrator()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsync("/api/moderation/users/user-1/suspend", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/user-1");
        Assert.NotNull(profile);
        Assert.False(profile.Aktivan);
    }

    [Fact]
    public async Task SuspendUser_ReturnsNotFound_WhenUserDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory, "administrator");

        var response = await client.PutAsync("/api/moderation/users/missing/suspend", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpClient CreateAuthenticatedClient(WebApplicationFactory<Program> factory, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("665000000000000000000801", role));

        return client;
    }

    private static WebApplicationFactory<Program> CreateFactory(
        FakeRepository<Post>? postsRepository = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<Post>>();
                    services.RemoveAll<IRepository<User>>();

                    services.AddSingleton<IRepository<Post>>(postsRepository ?? CreatePostsRepository());

                    services.AddSingleton<IRepository<User>>(new FakeRepository<User>(
                    [
                        new User
                        {
                            Id = "user-1",
                            Username = "baduser",
                            Email = "baduser@example.com",
                            PasswordHash = "hash",
                            Uloga = "registrovani",
                            Aktivan = true,
                            DatumReg = new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
                            FavoritniTimovi = []
                        }
                    ]));
                });
            });
    }

    private static FakeRepository<Post> CreatePostsRepository()
    {
        return new FakeRepository<Post>(
        [
            new Post
            {
                Id = "discussion-1",
                AutorId = "665000000000000000000501",
                Naslov = "Tema za moderaciju",
                Sadrzaj = "Ova tema ce biti uklonjena.",
                Tip = "diskusija",
                DatumKreiranja = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc),
                Obrisan = false
            }
        ]);
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
                new Claim(ClaimTypes.Name, "moderator"),
                new Claim(ClaimTypes.Email, "moderator@example.com"),
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
