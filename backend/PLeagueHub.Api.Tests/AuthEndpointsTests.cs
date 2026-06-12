using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Tests;

public sealed class AuthEndpointsTests
{
    [Fact]
    public async Task Register_CreatesUserAndReturnsJwtWithRole()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "marko",
            Email = "marko@example.com",
            Password = "StrongPass123!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal("marko", auth.Username);
        Assert.Equal("registrovani", auth.Uloga);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(auth.Token);
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.NameIdentifier && claim.Value == auth.UserId);
        Assert.Contains(jwt.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "registrovani");
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var request = new RegisterRequest
        {
            Username = "marko",
            Email = "marko@example.com",
            Password = "StrongPass123!"
        };

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", request);
        var secondResponse = await client.PostAsJsonAsync("/api/auth/register", request with { Username = "marko2" });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsJwt_WhenCredentialsAreValid()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var registerRequest = new RegisterRequest
        {
            Username = "marko",
            Email = "marko@example.com",
            Password = "StrongPass123!"
        };

        await client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            EmailOrUsername = "marko@example.com",
            Password = "StrongPass123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal("marko@example.com", auth.Email);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsInvalid()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "marko",
            Email = "marko@example.com",
            Password = "StrongPass123!"
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            EmailOrUsername = "marko@example.com",
            Password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUserById_ReturnsPublicProfileWithoutPasswordHash()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = "marko",
            Email = "marko@example.com",
            Password = "StrongPass123!"
        });
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();

        var profile = await client.GetFromJsonAsync<UserProfileResponse>($"/api/users/{auth!.UserId}");

        Assert.NotNull(profile);
        Assert.Equal("marko", profile.Username);
        Assert.Equal("marko@example.com", profile.Email);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<User>>();
                    services.AddSingleton<IRepository<User>>(new FakeRepository<User>());
                });
            });
    }

    private sealed class FakeRepository<TDocument> : IRepository<TDocument>
        where TDocument : BaseDocument
    {
        private readonly List<TDocument> _documents = [];

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
            throw new NotSupportedException();
        }

        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
