using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Data.Seeding;

public sealed class DatabaseSeeder
{
    private readonly IRepository<Match> _matchesRepository;
    private readonly IRepository<Player> _playersRepository;
    private readonly IRepository<Post> _postsRepository;
    private readonly IRepository<Statistic> _statisticsRepository;
    private readonly IRepository<Team> _teamsRepository;

    public DatabaseSeeder(
        IRepository<Team> teamsRepository,
        IRepository<Player> playersRepository,
        IRepository<Match> matchesRepository,
        IRepository<Statistic> statisticsRepository,
        IRepository<Post> postsRepository)
    {
        _teamsRepository = teamsRepository;
        _playersRepository = playersRepository;
        _matchesRepository = matchesRepository;
        _statisticsRepository = statisticsRepository;
        _postsRepository = postsRepository;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCollectionAsync(_teamsRepository, CreateTeams(), cancellationToken);
        await SeedCollectionAsync(_playersRepository, CreatePlayers(), cancellationToken);
        await SeedCollectionAsync(_matchesRepository, CreateMatches(), cancellationToken);
        await SeedCollectionAsync(_statisticsRepository, CreateStatistics(), cancellationToken);
        await SeedCollectionAsync(_postsRepository, CreatePosts(), cancellationToken);
    }

    private static async Task SeedCollectionAsync<TDocument>(
        IRepository<TDocument> repository,
        IReadOnlyCollection<TDocument> documents,
        CancellationToken cancellationToken)
        where TDocument : BaseDocument
    {
        var existingDocuments = await repository.GetAllAsync(cancellationToken);

        if (existingDocuments.Count > 0)
        {
            return;
        }

        foreach (var document in documents)
        {
            await repository.CreateAsync(document, cancellationToken);
        }
    }

    private static IReadOnlyCollection<Team> CreateTeams()
    {
        return
        [
            new Team
            {
                Id = "665000000000000000000001",
                Naziv = "Arsenal",
                Skracenica = "ARS",
                Stadion = "Emirates Stadium",
                Osnovan = 1886,
                LogoUrl = "https://resources.premierleague.com/premierleague/badges/t3.svg",
                Bodovi = 18,
                Pozicija = 1
            },
            new Team
            {
                Id = "665000000000000000000002",
                Naziv = "Liverpool",
                Skracenica = "LIV",
                Stadion = "Anfield",
                Osnovan = 1892,
                LogoUrl = "https://resources.premierleague.com/premierleague/badges/t14.svg",
                Bodovi = 17,
                Pozicija = 2
            },
            new Team
            {
                Id = "665000000000000000000003",
                Naziv = "Manchester City",
                Skracenica = "MCI",
                Stadion = "Etihad Stadium",
                Osnovan = 1880,
                LogoUrl = "https://resources.premierleague.com/premierleague/badges/t43.svg",
                Bodovi = 16,
                Pozicija = 3
            },
            new Team
            {
                Id = "665000000000000000000004",
                Naziv = "Chelsea",
                Skracenica = "CHE",
                Stadion = "Stamford Bridge",
                Osnovan = 1905,
                LogoUrl = "https://resources.premierleague.com/premierleague/badges/t8.svg",
                Bodovi = 13,
                Pozicija = 4
            },
            new Team
            {
                Id = "665000000000000000000005",
                Naziv = "Tottenham Hotspur",
                Skracenica = "TOT",
                Stadion = "Tottenham Hotspur Stadium",
                Osnovan = 1882,
                LogoUrl = "https://resources.premierleague.com/premierleague/badges/t6.svg",
                Bodovi = 12,
                Pozicija = 5
            },
            new Team
            {
                Id = "665000000000000000000006",
                Naziv = "Manchester United",
                Skracenica = "MUN",
                Stadion = "Old Trafford",
                Osnovan = 1878,
                LogoUrl = "https://resources.premierleague.com/premierleague/badges/t1.svg",
                Bodovi = 10,
                Pozicija = 6
            }
        ];
    }

    private static IReadOnlyCollection<Player> CreatePlayers()
    {
        return
        [
            new Player
            {
                Id = "665000000000000000000101",
                TeamId = "665000000000000000000001",
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
                Id = "665000000000000000000102",
                TeamId = "665000000000000000000002",
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
                Id = "665000000000000000000103",
                TeamId = "665000000000000000000003",
                Ime = "Erling",
                Prezime = "Haaland",
                Pozicija = "ST",
                Nacionalnost = "Norway",
                Golovi = 9,
                Asistencije = 1,
                Ocena = 8.4
            },
            new Player
            {
                Id = "665000000000000000000104",
                TeamId = "665000000000000000000004",
                Ime = "Cole",
                Prezime = "Palmer",
                Pozicija = "AM",
                Nacionalnost = "England",
                Golovi = 4,
                Asistencije = 5,
                Ocena = 8.1
            },
            new Player
            {
                Id = "665000000000000000000105",
                TeamId = "665000000000000000000005",
                Ime = "Son",
                Prezime = "Heung-min",
                Pozicija = "LW",
                Nacionalnost = "South Korea",
                Golovi = 6,
                Asistencije = 2,
                Ocena = 8.0
            },
            new Player
            {
                Id = "665000000000000000000106",
                TeamId = "665000000000000000000006",
                Ime = "Bruno",
                Prezime = "Fernandes",
                Pozicija = "AM",
                Nacionalnost = "Portugal",
                Golovi = 3,
                Asistencije = 6,
                Ocena = 7.9
            }
        ];
    }

    private static IReadOnlyCollection<Match> CreateMatches()
    {
        return
        [
            new Match
            {
                Id = "665000000000000000000201",
                DomacinId = "665000000000000000000001",
                GostId = "665000000000000000000004",
                Datum = new DateTime(2026, 8, 15, 16, 0, 0, DateTimeKind.Utc),
                Kolo = 1,
                Sezona = "2026/27",
                GolDomacin = 2,
                GolGost = 1,
                Status = "zavrsena"
            },
            new Match
            {
                Id = "665000000000000000000202",
                DomacinId = "665000000000000000000002",
                GostId = "665000000000000000000006",
                Datum = new DateTime(2026, 8, 16, 15, 30, 0, DateTimeKind.Utc),
                Kolo = 1,
                Sezona = "2026/27",
                GolDomacin = 3,
                GolGost = 2,
                Status = "zavrsena"
            },
            new Match
            {
                Id = "665000000000000000000203",
                DomacinId = "665000000000000000000003",
                GostId = "665000000000000000000005",
                Datum = new DateTime(2026, 8, 23, 17, 30, 0, DateTimeKind.Utc),
                Kolo = 2,
                Sezona = "2026/27",
                GolDomacin = null,
                GolGost = null,
                Status = "zakazana"
            }
        ];
    }

    private static IReadOnlyCollection<Statistic> CreateStatistics()
    {
        return
        [
            new Statistic
            {
                Id = "665000000000000000000301",
                MatchId = "665000000000000000000201",
                PlayerId = "665000000000000000000101",
                Golovi = 1,
                Asistencije = 1,
                Kartoni = 0,
                MinutiIgre = 90,
                Ocena = 8.7
            },
            new Statistic
            {
                Id = "665000000000000000000302",
                MatchId = "665000000000000000000202",
                PlayerId = "665000000000000000000102",
                Golovi = 2,
                Asistencije = 0,
                Kartoni = 0,
                MinutiIgre = 90,
                Ocena = 9.0
            },
            new Statistic
            {
                Id = "665000000000000000000303",
                MatchId = "665000000000000000000202",
                PlayerId = "665000000000000000000106",
                Golovi = 1,
                Asistencije = 1,
                Kartoni = 1,
                MinutiIgre = 90,
                Ocena = 7.8
            }
        ];
    }

    private static IReadOnlyCollection<Post> CreatePosts()
    {
        return
        [
            new Post
            {
                Id = "665000000000000000000401",
                AutorId = "665000000000000000000501",
                Naslov = "Nova sezona Premier League startuje uz velike derbije",
                Sadrzaj = "PLeague Hub prati rezultate, tabelu i statistiku igraca tokom cele sezone.",
                Tip = "vest",
                DatumKreiranja = new DateTime(2026, 8, 14, 10, 0, 0, DateTimeKind.Utc),
                Obrisan = false
            },
            new Post
            {
                Id = "665000000000000000000402",
                AutorId = "665000000000000000000501",
                Naslov = "Ko je favorit za titulu ove sezone?",
                Sadrzaj = "Diskutujte o formi klubova, transferima i prvim utiscima nakon uvodnih kola.",
                Tip = "diskusija",
                DatumKreiranja = new DateTime(2026, 8, 17, 12, 30, 0, DateTimeKind.Utc),
                Obrisan = false
            }
        ];
    }
}
