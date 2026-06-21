using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Tests;

public sealed class BackendInfrastructureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BackendInfrastructureTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SwaggerJsonEndpoint_IsAvailable_ForManualApiTesting()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PLeague Hub API", body);
    }

    [Fact]
    public async Task SwaggerJsonEndpoint_DescribesJwtBearerAuthorization()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"securitySchemes\"", body);
        Assert.Contains("\"Bearer\"", body);
        Assert.Contains("\"scheme\":\"bearer\"", body.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsServiceStatus_ForOperationalChecks()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.Equal("healthy", root.GetProperty("status").GetString());
        Assert.Equal("PLeague Hub API", root.GetProperty("service").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("environment").GetString()));
        Assert.True(root.TryGetProperty("checkedAtUtc", out _));
    }

    [Fact]
    public async Task CorsPolicy_AllowsLocalReactFrontend()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/health");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins));
        Assert.Contains("http://localhost:3000", origins);
    }

    [Fact]
    public void GenericRepository_IsRegistered_ForDomainDocuments()
    {
        var apiAssembly = typeof(Team).Assembly;
        var repositoryInterface = apiAssembly.GetType("PLeagueHub.Api.Repositories.IRepository`1");
        var repositoryImplementation = apiAssembly.GetType("PLeagueHub.Api.Repositories.MongoRepository`1");

        Assert.NotNull(repositoryInterface);
        Assert.NotNull(repositoryImplementation);

        var closedServiceType = repositoryInterface!.MakeGenericType(typeof(Team));
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetService(closedServiceType);

        Assert.NotNull(repository);
        Assert.Equal(repositoryImplementation!.MakeGenericType(typeof(Team)), repository!.GetType());
    }

    [Fact]
    public void RepositoryInterface_ExposesAsyncCrudContract()
    {
        var apiAssembly = typeof(Team).Assembly;
        var repositoryInterface = apiAssembly.GetType("PLeagueHub.Api.Repositories.IRepository`1");

        Assert.NotNull(repositoryInterface);

        var methodNames = repositoryInterface!
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name)
            .ToArray();

        Assert.Contains("GetAllAsync", methodNames);
        Assert.Contains("GetByIdAsync", methodNames);
        Assert.Contains("CreateAsync", methodNames);
        Assert.Contains("UpdateAsync", methodNames);
        Assert.Contains("DeleteAsync", methodNames);
    }

    [Fact]
    public async Task MongoRepository_ReturnsNotFoundSemantics_ForInvalidObjectId()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Team>>();

        var document = await repository.GetByIdAsync("not-a-valid-object-id");
        var updated = await repository.UpdateAsync("not-a-valid-object-id", new Team());
        var deleted = await repository.DeleteAsync("not-a-valid-object-id");

        Assert.Null(document);
        Assert.False(updated);
        Assert.False(deleted);
    }

    [Fact]
    public async Task MongoRepository_UpdateSucceeds_WhenDocumentIsUnchanged()
    {
        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRepository<Team>>();
        var team = await repository.CreateAsync(new Team
        {
            Naziv = $"Repository test {Guid.NewGuid():N}",
            Skracenica = "TST",
            Stadion = "Test Stadium",
            Osnovan = 2026,
            LogoUrl = "https://example.com/test.svg",
            Bodovi = 0,
            Pozicija = 99
        });

        try
        {
            var updated = await repository.UpdateAsync(team.Id!, team);

            Assert.True(updated);
        }
        finally
        {
            await repository.DeleteAsync(team.Id!);
        }
    }
}
