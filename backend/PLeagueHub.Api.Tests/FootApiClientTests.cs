using System.Net;
using System.Net.Http.Headers;
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

    [Fact]
    public async Task GetTeamStandingsAsync_MapsFootApiRows()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return JsonResponse("""
                {"standings":[{"rows":[
                  {"team":{"id":42,"name":"Arsenal","nameCode":"ARS"},"position":1,"points":85}
                ]}]}
                """);
        });
        var client = CreateClient(handler, apiKey: "test-key");

        var standings = await client.GetTeamStandingsAsync(17, 76986);

        Assert.NotNull(capturedRequest);
        Assert.Equal(
            "https://footapi7.p.rapidapi.com/api/tournament/17/season/76986/standings/total",
            capturedRequest.RequestUri?.AbsoluteUri);
        Assert.Equal("test-key", capturedRequest.Headers.GetValues("X-RapidAPI-Key").Single());
        var arsenal = Assert.Single(standings);
        Assert.Equal(42, arsenal.ProviderId);
        Assert.Equal("Arsenal", arsenal.Name);
        Assert.Equal("ARS", arsenal.Abbreviation);
        Assert.Equal(1, arsenal.Position);
        Assert.Equal(85, arsenal.Points);
    }

    [Fact]
    public async Task GetTeamLogoAsync_ReturnsValidatedImageBytes()
    {
        HttpRequestMessage? capturedRequest = null;
        var expectedBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return ImageResponse(expectedBytes, "image/png");
        });
        var client = CreateClient(handler, apiKey: "test-key");

        var logo = await client.GetTeamLogoAsync(60);

        Assert.NotNull(capturedRequest);
        Assert.Equal("https://footapi7.p.rapidapi.com/api/team/60/image", capturedRequest.RequestUri?.AbsoluteUri);
        Assert.Equal("test-key", capturedRequest.Headers.GetValues("X-RapidAPI-Key").Single());
        Assert.Equal("image/png", logo.ContentType);
        Assert.Equal(expectedBytes, logo.Content);
    }

    [Fact]
    public async Task GetTeamLogoAsync_RejectsNonImageContent()
    {
        var client = CreateClient(
            new StubHttpMessageHandler(_ => ImageResponse([1, 2, 3], "text/html")),
            apiKey: "test-key");

        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetTeamLogoAsync(60));
    }

    [Fact]
    public async Task GetTeamLogoAsync_RejectsEmptyContent()
    {
        var client = CreateClient(
            new StubHttpMessageHandler(_ => ImageResponse([], "image/png")),
            apiKey: "test-key");

        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetTeamLogoAsync(60));
    }

    [Fact]
    public async Task GetTeamLogoAsync_RejectsContentLargerThanOneMegabyte()
    {
        var client = CreateClient(
            new StubHttpMessageHandler(_ => ImageResponse(new byte[1_048_577], "image/png")),
            apiKey: "test-key");

        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetTeamLogoAsync(60));
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

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage ImageResponse(byte[] content, string contentType)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return response;
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
