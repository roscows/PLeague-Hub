using System.Linq.Expressions;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Tests;

public sealed class PublicQueryFilterTests
{
    [Fact]
    public async Task GetMatches_FiltersBySeasonStatusAndRound()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var matches = await client.GetFromJsonAsync<List<Match>>(
            "/api/matches?season=2026%2F27&status=zakazana&round=2");

        Assert.NotNull(matches);
        Assert.Single(matches);
        Assert.Equal("match-2", matches[0].Id);
    }

    [Fact]
    public async Task GetPlayers_FiltersByTeamPositionAndSearch()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var players = await client.GetFromJsonAsync<List<Player>>(
            "/api/players?teamId=team-1&position=RW&search=sak");

        Assert.NotNull(players);
        Assert.Single(players);
        Assert.Equal("player-1", players[0].Id);
    }

    [Fact]
    public async Task GetTeams_SearchesByNameOrAbbreviation()
    {
        using var factory = CreateFactory();
        var client = factory.CreateClient();

        var teams = await client.GetFromJsonAsync<List<Team>>("/api/teams?search=liv");

        Assert.NotNull(teams);
        Assert.Single(teams);
        Assert.Equal("Liverpool", teams[0].Naziv);
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
                        },
                        new Match
                        {
                            Id = "match-2",
                            DomacinId = "team-2",
                            GostId = "team-1",
                            Datum = new DateTime(2026, 8, 23, 17, 30, 0, DateTimeKind.Utc),
                            Kolo = 2,
                            Sezona = "2026/27",
                            GolDomacin = null,
                            GolGost = null,
                            Status = "zakazana"
                        },
                        new Match
                        {
                            Id = "match-3",
                            DomacinId = "team-3",
                            GostId = "team-1",
                            Datum = new DateTime(2025, 8, 23, 17, 30, 0, DateTimeKind.Utc),
                            Kolo = 2,
                            Sezona = "2025/26",
                            GolDomacin = null,
                            GolGost = null,
                            Status = "zakazana"
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
                            Bodovi = 18,
                            Pozicija = 1
                        },
                        new Team
                        {
                            Id = "team-2",
                            Naziv = "Liverpool",
                            Skracenica = "LIV",
                            Stadion = "Anfield",
                            Osnovan = 1892,
                            LogoUrl = "https://example.com/liverpool.png",
                            Bodovi = 17,
                            Pozicija = 2
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
                            Golovi = 5,
                            Asistencije = 4,
                            Ocena = 8.2
                        },
                        new Player
                        {
                            Id = "player-2",
                            TeamId = "team-2",
                            Ime = "Mohamed",
                            Prezime = "Salah",
                            Pozicija = "RW",
                            Nacionalnost = "Egypt",
                            Golovi = 7,
                            Asistencije = 3,
                            Ocena = 8.5
                        },
                        new Player
                        {
                            Id = "player-3",
                            TeamId = "team-1",
                            Ime = "Declan",
                            Prezime = "Rice",
                            Pozicija = "CM",
                            Nacionalnost = "England",
                            Golovi = 1,
                            Asistencije = 2,
                            Ocena = 7.7
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
