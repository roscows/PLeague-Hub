using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Tests;

public sealed class ForumEndpointContractTests
{
    private const string JwtSecret = "PLeagueHub.Dev.Secret.Key.For.Jwt.Auth.ChangeMe.2026!";
    private const string UserId = "665000000000000000000503";

    [Fact]
    public async Task GetTopics_BindsSearchAndPagingAndReturnsEnvelope()
    {
        var service = new FakeForumService();
        using var factory = CreateFactory(service);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/forum?search=ars&page=2&pageSize=10");
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<ForumTopicResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(page);
        Assert.Equal("ars", service.LastListRequest?.Search);
        Assert.Equal(2, service.LastListRequest?.Page);
        Assert.Equal(10, service.LastListRequest?.PageSize);
    }

    [Fact]
    public async Task Vote_ReturnsUnauthorizedWithoutJwt()
    {
        using var factory = CreateFactory(new FakeForumService());
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            "/api/forum/comments/665000000000000000000811/vote",
            new VoteCommentRequest(1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Vote_ReturnsUpdatedTotalsForAuthenticatedUser()
    {
        using var factory = CreateFactory(new FakeForumService());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        var response = await client.PutAsJsonAsync(
            "/api/forum/comments/665000000000000000000811/vote",
            new VoteCommentRequest(-1));
        var vote = await response.Content.ReadFromJsonAsync<ForumVoteResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(-1, vote?.TrenutniGlas);
        Assert.Equal(1, vote?.Dislajkovi);
    }

    [Fact]
    public async Task CreateDiscussion_ReturnsCreatedForAuthenticatedUser()
    {
        using var factory = CreateFactory(new FakeForumService());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        var response = await client.PostAsJsonAsync("/api/forum", new CreateForumPostRequest
        {
            Naslov = "Nova tema",
            Sadrzaj = "Sadrzaj nove teme"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/forum/665000000000000000000801", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CreateComment_ForwardsParentAndReturnsCreated()
    {
        var service = new FakeForumService();
        using var factory = CreateFactory(service);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        var response = await client.PostAsJsonAsync(
            "/api/forum/665000000000000000000801/comments",
            new CreateCommentRequest
            {
                Tekst = "Odgovor",
                ParentCommentId = "665000000000000000000811"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("665000000000000000000811", service.LastCommentRequest?.ParentCommentId);
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeForumService service)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IForumService>();
                    services.AddSingleton<IForumService>(service);
                });
            });
    }

    private static string CreateToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var token = new JwtSecurityToken(
            issuer: "PLeagueHub",
            audience: "PLeagueHub",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, UserId),
                new Claim(ClaimTypes.Name, "fan"),
                new Claim(ClaimTypes.Role, "registrovani")
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FakeForumService : IForumService
    {
        public ForumListRequest? LastListRequest { get; private set; }

        public CreateCommentRequest? LastCommentRequest { get; private set; }

        public Task<PagedResponse<ForumTopicResponse>> GetTopicsAsync(ForumListRequest request, CancellationToken cancellationToken = default)
        {
            LastListRequest = request;
            return Task.FromResult(new PagedResponse<ForumTopicResponse>([], request.Page, request.PageSize, 0, 0));
        }

        public Task<ForumDiscussionResponse?> GetDiscussionAsync(string id, CancellationToken cancellationToken = default)
            => Task.FromResult<ForumDiscussionResponse?>(null);

        public Task<IReadOnlyList<ForumCommentResponse>?> GetCommentsAsync(string postId, string? currentUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ForumCommentResponse>?>([]);

        public Task<ForumResult<ForumDiscussionResponse>> CreateDiscussionAsync(CreateForumPostRequest request, string? authorId, CancellationToken cancellationToken = default)
            => Task.FromResult(ForumResult<ForumDiscussionResponse>.Success(
                new ForumDiscussionResponse(
                    "665000000000000000000801",
                    request.Naslov,
                    request.Sadrzaj,
                    authorId!,
                    "fan",
                    "registrovani",
                    DateTime.UtcNow,
                    false)));

        public Task<ForumResult<ForumCommentResponse>> CreateCommentAsync(string postId, CreateCommentRequest request, string? authorId, CancellationToken cancellationToken = default)
        {
            LastCommentRequest = request;
            return Task.FromResult(ForumResult<ForumCommentResponse>.Success(
                new ForumCommentResponse(
                    "665000000000000000000812",
                    postId,
                    request.ParentCommentId,
                    authorId!,
                    "fan",
                    "registrovani",
                    request.Tekst,
                    DateTime.UtcNow,
                    false,
                    1,
                    0,
                    0,
                    null)));
        }

        public Task<ForumResult<ForumVoteResponse>> VoteAsync(string commentId, string? userId, int value, CancellationToken cancellationToken = default)
            => Task.FromResult(ForumResult<ForumVoteResponse>.Success(
                new ForumVoteResponse(commentId, value == 1 ? 1 : 0, value == -1 ? 1 : 0, value)));

        public Task<ForumResult<ForumVoteResponse>> RemoveVoteAsync(string commentId, string? userId, CancellationToken cancellationToken = default)
            => Task.FromResult(ForumResult<ForumVoteResponse>.Success(new ForumVoteResponse(commentId, 0, 0, null)));
    }
}
