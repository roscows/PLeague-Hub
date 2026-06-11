using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Tests;

public sealed class PublicReadEndpointsTests
{
    [Fact]
    public async Task GetMatches_ReturnsAllMatches()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var matches = await client.GetFromJsonAsync<List<Match>>("/api/matches");

        Assert.NotNull(matches);
        Assert.Single(matches);
        Assert.Equal("match-1", matches[0].Id);
    }

    [Fact]
    public async Task GetMatchById_ReturnsMatch_WhenItExists()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var match = await client.GetFromJsonAsync<Match>("/api/matches/match-1");

        Assert.NotNull(match);
        Assert.Equal("match-1", match.Id);
    }

    [Fact]
    public async Task GetMatchById_ReturnsNotFound_WhenItDoesNotExist()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/matches/missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetTeams_ReturnsAllTeams()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var teams = await client.GetFromJsonAsync<List<Team>>("/api/teams");

        Assert.NotNull(teams);
        Assert.Single(teams);
        Assert.Equal("Arsenal", teams[0].Naziv);
    }

    [Fact]
    public async Task GetTeamById_ReturnsTeam_WhenItExists()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var team = await client.GetFromJsonAsync<Team>("/api/teams/team-1");

        Assert.NotNull(team);
        Assert.Equal("team-1", team.Id);
    }

    [Fact]
    public async Task GetPlayers_ReturnsAllPlayers()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var players = await client.GetFromJsonAsync<List<Player>>("/api/players");

        Assert.NotNull(players);
        Assert.Single(players);
        Assert.Equal("Bukayo", players[0].Ime);
    }

    [Fact]
    public async Task GetPlayerById_ReturnsPlayer_WhenItExists()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var player = await client.GetFromJsonAsync<Player>("/api/players/player-1");

        Assert.NotNull(player);
        Assert.Equal("player-1", player.Id);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IRepository<Match>>();
                    services.RemoveAll<IRepository<Team>>();
                    services.RemoveAll<IRepository<Player>>();

                    services.AddSingleton<IRepository<Match>>(new FakeRepository<Match>(
                    [
                        new Match
                        {
                            Id = "match-1",
                            DomacinId = "team-1",
                            GostId = "team-2",
                            Datum = new DateTime(2026, 8, 15, 16, 0, 0, DateTimeKind.Utc),
                            Kolo = 1,
                            Sezona = "2026/27",
                            GolDomacin = 2,
                            GolGost = 1,
                            Status = "zavrsena"
                        }
                    ]));

                    services.AddSingleton<IRepository<Team>>(new FakeRepository<Team>(
                    [
                        new Team
                        {
                            Id = "team-1",
                            Naziv = "Arsenal",
                            Skracenica = "ARS",
                            Stadion = "Emirates Stadium",
                            Osnovan = 1886,
                            LogoUrl = "https://example.com/arsenal.png",
                            Bodovi = 3,
                            Pozicija = 1
                        }
                    ]));

                    services.AddSingleton<IRepository<Player>>(new FakeRepository<Player>(
                    [
                        new Player
                        {
                            Id = "player-1",
                            TeamId = "team-1",
                            Ime = "Bukayo",
                            Prezime = "Saka",
                            Pozicija = "RW",
                            Nacionalnost = "England",
                            Golovi = 1,
                            Asistencije = 1,
                            Ocena = 8.3
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
