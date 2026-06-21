using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Tests;

public sealed class StatisticsEndpointsTests
{
    [Fact]
    public async Task GetStatistics_FiltersByMatchId()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var statistics = await client.GetFromJsonAsync<List<Statistic>>("/api/statistics?matchId=match-1");

        Assert.NotNull(statistics);
        Assert.Equal(2, statistics.Count);
        Assert.All(statistics, statistic => Assert.Equal("match-1", statistic.MatchId));
    }

    [Fact]
    public async Task GetStatistics_FiltersByPlayerId()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var statistics = await client.GetFromJsonAsync<List<Statistic>>("/api/statistics?playerId=player-2");

        Assert.NotNull(statistics);
        Assert.Single(statistics);
        Assert.Equal("stat-2", statistics[0].Id);
    }

    [Fact]
    public async Task GetStatisticById_ReturnsStatistic_WhenItExists()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var statistic = await client.GetFromJsonAsync<Statistic>("/api/statistics/stat-1");

        Assert.NotNull(statistic);
        Assert.Equal("stat-1", statistic.Id);
    }

    [Fact]
    public async Task GetStatisticById_ReturnsNotFound_WhenItDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/statistics/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<Statistic>>();
                    services.AddSingleton<IRepository<Statistic>>(new FakeRepository<Statistic>(
                    [
                        new Statistic
                        {
                            Id = "stat-1",
                            MatchId = "match-1",
                            PlayerId = "player-1",
                            Golovi = 1,
                            Asistencije = 1,
                            Kartoni = 0,
                            MinutiIgre = 90,
                            Ocena = 8.7
                        },
                        new Statistic
                        {
                            Id = "stat-2",
                            MatchId = "match-1",
                            PlayerId = "player-2",
                            Golovi = 2,
                            Asistencije = 0,
                            Kartoni = 0,
                            MinutiIgre = 90,
                            Ocena = 9.0
                        },
                        new Statistic
                        {
                            Id = "stat-3",
                            MatchId = "match-2",
                            PlayerId = "player-1",
                            Golovi = 0,
                            Asistencije = 1,
                            Kartoni = 1,
                            MinutiIgre = 90,
                            Ocena = 7.8
                        }
                    ]));
                });
            });
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
            throw new NotSupportedException();
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
