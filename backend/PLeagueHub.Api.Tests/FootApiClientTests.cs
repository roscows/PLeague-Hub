using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PLeagueHub.Api.Configuration;
using PLeagueHub.Api.Services.Football;

namespace PLeagueHub.Api.Tests;

public sealed class FootApiClientTests
{
    [Fact]
    public async Task SearchAsync_SendsEncodedTermAndRapidApiHeaders()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
            };
        });
        var client = CreateClient(handler, apiKey: "test-key");

        using var result = await client.SearchAsync("Premier League");

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest.Method);
        Assert.Equal("https://footapi7.p.rapidapi.com/api/search/Premier%20League", capturedRequest.RequestUri?.AbsoluteUri);
        Assert.Equal("test-key", capturedRequest.Headers.GetValues("X-RapidAPI-Key").Single());
        Assert.Equal("footapi7.p.rapidapi.com", capturedRequest.Headers.GetValues("X-RapidAPI-Host").Single());
        Assert.Equal(JsonValueKind.Array, result.RootElement.GetProperty("results").ValueKind);
    }

    [Fact]
    public async Task SearchAsync_ThrowsBeforeRequest_WhenApiKeyIsMissing()
    {
        var requestCount = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateClient(handler, apiKey: "   ");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SearchAsync("Arsenal"));

        Assert.Contains("FootApi:ApiKey", exception.Message);
        Assert.Equal(0, requestCount);
    }

    private static FootApiClient CreateClient(HttpMessageHandler handler, string apiKey)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://footapi7.p.rapidapi.com")
        };
        var settings = Options.Create(new FootApiSettings
        {
            ApiKey = apiKey,
            Host = "footapi7.p.rapidapi.com",
            BaseUrl = "https://footapi7.p.rapidapi.com"
        });

        return new FootApiClient(httpClient, settings);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
