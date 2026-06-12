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

public sealed class ForumWriteEndpointsTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";
    private const string UserId = "665000000000000000000701";

    [Fact]
    public async Task CreateDiscussion_ReturnsUnauthorized_WhenTokenIsMissing()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/forum", new CreateForumPostRequest
        {
            Naslov = "Nova tema",
            Sadrzaj = "Sta mislite o narednom kolu?"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateDiscussion_CreatesVisibleDiscussion_WhenUserIsAuthenticated()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/forum", new CreateForumPostRequest
        {
            Naslov = "Nova tema",
            Sadrzaj = "Sta mislite o narednom kolu?"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var discussions = await client.GetFromJsonAsync<List<Post>>("/api/forum");
        Assert.NotNull(discussions);
        Assert.Contains(discussions, post =>
            post.Naslov == "Nova tema"
            && post.Tip == "diskusija"
            && post.AutorId == UserId
            && !post.Obrisan);
    }

    [Fact]
    public async Task CreateComment_CreatesComment_WhenDiscussionExists()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/forum/discussion-1/comments", new CreateCommentRequest
        {
            Tekst = "Slazem se, bice tesko gostovanje."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var comments = await client.GetFromJsonAsync<List<Comment>>("/api/forum/discussion-1/comments");
        Assert.NotNull(comments);
        Assert.Contains(comments, comment =>
            comment.PostId == "discussion-1"
            && comment.AutorId == UserId
            && comment.Tekst == "Slazem se, bice tesko gostovanje."
            && !comment.Obrisan);
    }

    [Fact]
    public async Task CreateComment_ReturnsNotFound_WhenDiscussionDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = CreateAuthenticatedClient(factory);

        var response = await client.PostAsJsonAsync("/api/forum/missing/comments", new CreateCommentRequest
        {
            Tekst = "Komentar bez teme."
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetComments_ReturnsOnlyVisibleComments_ForDiscussion()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var comments = await client.GetFromJsonAsync<List<Comment>>("/api/forum/discussion-1/comments");

        Assert.NotNull(comments);
        Assert.Single(comments);
        Assert.Equal("comment-1", comments[0].Id);
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
                    services.RemoveAll<IRepository<Post>>();
                    services.RemoveAll<IRepository<Comment>>();

                    services.AddSingleton<IRepository<Post>>(new FakeRepository<Post>(
                    [
                        new Post
                        {
                            Id = "discussion-1",
                            AutorId = "665000000000000000000501",
                            Naslov = "Postojeca tema",
                            Sadrzaj = "Tema koja postoji pre testa.",
                            Tip = "diskusija",
                            DatumKreiranja = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc),
                            Obrisan = false
                        },
                        new Post
                        {
                            Id = "deleted-discussion",
                            AutorId = "665000000000000000000501",
                            Naslov = "Obrisana tema",
                            Sadrzaj = "Na ovu temu ne treba komentarisati.",
                            Tip = "diskusija",
                            DatumKreiranja = new DateTime(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc),
                            Obrisan = true
                        }
                    ]));

                    services.AddSingleton<IRepository<Comment>>(new FakeRepository<Comment>(
                    [
                        new Comment
                        {
                            Id = "comment-1",
                            PostId = "discussion-1",
                            AutorId = "665000000000000000000601",
                            Tekst = "Vidljiv komentar.",
                            DatumKreiranja = new DateTime(2026, 8, 11, 13, 0, 0, DateTimeKind.Utc),
                            Obrisan = false
                        },
                        new Comment
                        {
                            Id = "deleted-comment",
                            PostId = "discussion-1",
                            AutorId = "665000000000000000000602",
                            Tekst = "Obrisan komentar.",
                            DatumKreiranja = new DateTime(2026, 8, 11, 14, 0, 0, DateTimeKind.Utc),
                            Obrisan = true
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
                new Claim(ClaimTypes.Name, "user"),
                new Claim(ClaimTypes.Email, "user@example.com"),
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
