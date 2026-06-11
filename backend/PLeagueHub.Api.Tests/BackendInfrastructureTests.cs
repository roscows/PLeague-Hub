using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PLeagueHub.Api.Models;

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
}
