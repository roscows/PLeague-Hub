using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Services;

namespace PLeagueHub.Api.Data.Seeding;

public sealed class DatabaseSeeder
{
    private readonly IRepository<Match> _matchesRepository;
    private readonly IRepository<Player> _playersRepository;
    private readonly IRepository<Post> _postsRepository;
    private readonly IRepository<Comment> _commentsRepository;
    private readonly IRepository<CommentVote> _commentVotesRepository;
    private readonly IRepository<Statistic> _statisticsRepository;
    private readonly IRepository<Team> _teamsRepository;
    private readonly IRepository<User> _usersRepository;
    private readonly IRepository<NewsSource>? _newsSourcesRepository;
    private readonly IPasswordService _passwordService;

    public DatabaseSeeder(
        IRepository<Team> teamsRepository,
        IRepository<Player> playersRepository,
        IRepository<Match> matchesRepository,
        IRepository<Statistic> statisticsRepository,
        IRepository<Post> postsRepository,
        IRepository<Comment> commentsRepository,
        IRepository<CommentVote> commentVotesRepository,
        IRepository<User> usersRepository,
        IPasswordService passwordService,
        IRepository<NewsSource>? newsSourcesRepository = null)
    {
        _teamsRepository = teamsRepository;
        _playersRepository = playersRepository;
        _matchesRepository = matchesRepository;
        _statisticsRepository = statisticsRepository;
        _postsRepository = postsRepository;
        _commentsRepository = commentsRepository;
        _commentVotesRepository = commentVotesRepository;
        _usersRepository = usersRepository;
        _passwordService = passwordService;
        _newsSourcesRepository = newsSourcesRepository;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seedNow = DateTime.UtcNow;
        await SeedCollectionAsync(_teamsRepository, CreateTeams(), cancellationToken);
        await SeedCollectionAsync(_playersRepository, CreatePlayers(), cancellationToken);
        await SeedCollectionAsync(_matchesRepository, CreateMatches(), cancellationToken);
        await SeedCollectionAsync(_statisticsRepository, CreateStatistics(), cancellationToken);
        await SeedMissingDocumentsAsync(_postsRepository, CreatePosts(seedNow), cancellationToken);
        await SeedUsersAsync(cancellationToken);
        await SeedMissingDocumentsAsync(_commentsRepository, CreateComments(seedNow), cancellationToken);
        await SeedCommentVotesAsync(cancellationToken);
        if (_newsSourcesRepository is not null)
            await SeedMissingDocumentsAsync(_newsSourcesRepository, CreateNewsSources(seedNow), cancellationToken);
    }

    private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        foreach (var user in CreateUsers())
        {
            var exists = await _usersRepository.ExistsAsync(
                existingUser => existingUser.Email == user.Email || existingUser.Username == user.Username,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            user.PasswordHash = _passwordService.HashPassword(user, "PLeague123!");
            await _usersRepository.CreateAsync(user, cancellationToken);
        }
    }

    private async Task SeedCommentVotesAsync(CancellationToken cancellationToken)
    {
        foreach (var vote in CreateCommentVotes())
        {
            var exists = await _commentVotesRepository.ExistsAsync(
                existing => existing.CommentId == vote.CommentId && existing.UserId == vote.UserId,
                cancellationToken);
            if (!exists) await _commentVotesRepository.CreateAsync(vote, cancellationToken);
        }
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

    private static async Task SeedMissingDocumentsAsync<TDocument>(
        IRepository<TDocument> repository,
        IReadOnlyCollection<TDocument> documents,
        CancellationToken cancellationToken)
        where TDocument : BaseDocument
    {
        foreach (var document in documents)
        {
            if (document.Id is null || await repository.ExistsAsync(
                    existing => existing.Id == document.Id,
                    cancellationToken))
            {
                continue;
            }

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

    private static IReadOnlyCollection<Post> CreatePosts(DateTime seedNow)
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
                DatumKreiranja = seedNow.AddDays(-7),
                PoslednjaAktivnost = seedNow.AddDays(-7),
                Obrisan = false
            },
            new Post
            {
                Id = "665000000000000000000402",
                AutorId = "665000000000000000000501",
                Naslov = "Ko je favorit za titulu ove sezone?",
                Sadrzaj = "Diskutujte o formi klubova, transferima i prvim utiscima nakon uvodnih kola.",
                Tip = "diskusija",
                DatumKreiranja = seedNow.AddDays(-4),
                PoslednjaAktivnost = seedNow.AddDays(-3).AddHours(-22),
                Obrisan = false
            },
            new Post
            {
                Id = "665000000000000000000403",
                AutorId = "665000000000000000000501",
                Naslov = "Pravila Premier League foruma",
                Sadrzaj = "Postujte sagovornike, drzite se teme i argumentujte svoje misljenje bez vredjanja.",
                Tip = "diskusija",
                DatumKreiranja = seedNow.AddHours(-26),
                PoslednjaAktivnost = seedNow.AddHours(-25).AddMinutes(-45),
                Istaknut = true
            },
            new Post
            {
                Id = "665000000000000000000404",
                AutorId = "665000000000000000000503",
                Naslov = "Ko osvaja Premier League sledece sezone?",
                Sadrzaj = "Da li aktuelni favoriti ostaju na vrhu ili nas ceka iznenadjenje?",
                Tip = "diskusija",
                DatumKreiranja = seedNow.AddHours(-4),
                PoslednjaAktivnost = seedNow.AddHours(-1).AddMinutes(-45)
            },
            new Post
            {
                Id = "665000000000000000000405",
                AutorId = "665000000000000000000502",
                Naslov = "Najbolji transfer ovog leta",
                Sadrzaj = "Koji klub je do sada uradio najbolji posao na trzistu?",
                Tip = "diskusija",
                DatumKreiranja = seedNow.AddHours(-4).AddMinutes(-35),
                PoslednjaAktivnost = seedNow.AddHours(-1).AddMinutes(-55)
            },
            new Post
            {
                Id = "665000000000000000000406",
                AutorId = "665000000000000000000503",
                Naslov = "Haaland ili Salah za kapitena?",
                Sadrzaj = "Koga birate za prvo kolo fantasy sezone i zasto?",
                Tip = "diskusija",
                DatumKreiranja = seedNow.AddHours(-5).AddMinutes(-30),
                PoslednjaAktivnost = seedNow.AddHours(-5).AddMinutes(-30)
            },
            new Post
            {
                Id = "665000000000000000000407",
                AutorId = "665000000000000000000501",
                Naslov = "Najpotcenjeniji vezista lige",
                Sadrzaj = "Ko radi najveci posao za ekipu, a ne dobija dovoljno paznje?",
                Tip = "diskusija",
                DatumKreiranja = seedNow.AddHours(-13).AddMinutes(-50),
                PoslednjaAktivnost = seedNow.AddHours(-13).AddMinutes(-50)
            },
            new Post
            {
                Id = "665000000000000000000408",
                AutorId = "665000000000000000000502",
                Naslov = "Prognoza prvog kola",
                Sadrzaj = "Ostavite rezultate svih utakmica prvog kola.",
                Tip = "diskusija",
                DatumKreiranja = seedNow.AddHours(-16).AddMinutes(-20),
                PoslednjaAktivnost = seedNow.AddHours(-16).AddMinutes(-20)
            },
            new Post
            {
                Id = "665000000000000000000409",
                AutorId = "665000000000000000000503",
                Naslov = "Koji stadion ima najbolju atmosferu?",
                Sadrzaj = "Anfield, St James Park, Selhurst Park ili neko cetvrti?",
                Tip = "diskusija",
                DatumKreiranja = seedNow.AddHours(-19).AddMinutes(-45),
                PoslednjaAktivnost = seedNow.AddHours(-19).AddMinutes(-45)
            },
            new Post
            {
                Id = "665000000000000000000410",
                AutorId = "665000000000000000000501",
                Naslov = "Mladi igraci koje treba pratiti",
                Sadrzaj = "Predlozite igrace do 21 godine koji bi mogli da naprave iskorak.",
                Tip = "diskusija",
                DatumKreiranja = seedNow.AddHours(-23),
                PoslednjaAktivnost = seedNow.AddHours(-23)
            }
        ];
    }

    private static IReadOnlyCollection<Comment> CreateComments(DateTime seedNow)
    {
        return
        [
            new Comment
            {
                Id = "665000000000000000000601",
                PostId = "665000000000000000000403",
                AutorId = "665000000000000000000501",
                Tekst = "Dobrodosli na forum. Kritika je dobrodosla, vredjanje nije.",
                DatumKreiranja = seedNow.AddHours(-25).AddMinutes(-45)
            },
            new Comment
            {
                Id = "665000000000000000000602",
                PostId = "665000000000000000000404",
                AutorId = "665000000000000000000503",
                Tekst = "Arsenal mi deluje najspremnije ako zadrzi kljucne igrace.",
                DatumKreiranja = seedNow.AddHours(-3).AddMinutes(-20)
            },
            new Comment
            {
                Id = "665000000000000000000603",
                PostId = "665000000000000000000404",
                AutorId = "665000000000000000000501",
                ParentCommentId = "665000000000000000000602",
                Tekst = "Slazem se za kontinuitet, ali City i dalje ima najdublji sastav.",
                DatumKreiranja = seedNow.AddHours(-2).AddMinutes(-55)
            },
            new Comment
            {
                Id = "665000000000000000000604",
                PostId = "665000000000000000000404",
                AutorId = "665000000000000000000503",
                ParentCommentId = "665000000000000000000603",
                Tekst = "Dubina je velika, ali ce i motivacija posle toliko sezona biti faktor.",
                DatumKreiranja = seedNow.AddHours(-1).AddMinutes(-45)
            },
            new Comment
            {
                Id = "665000000000000000000605",
                PostId = "665000000000000000000405",
                AutorId = "665000000000000000000502",
                Tekst = "Najbolji transfer nije uvek najskuplji, uklapanje u sistem je kljucno.",
                DatumKreiranja = seedNow.AddHours(-2).AddMinutes(-40)
            },
            new Comment
            {
                Id = "665000000000000000000606",
                PostId = "665000000000000000000405",
                AutorId = "665000000000000000000503",
                ParentCommentId = "665000000000000000000605",
                Tekst = "Tacno, posebno kod igraca koji odmah resavaju problem u prvih jedanaest.",
                DatumKreiranja = seedNow.AddHours(-2).AddMinutes(-15)
            },
            new Comment
            {
                Id = "665000000000000000000607",
                PostId = "665000000000000000000402",
                AutorId = "665000000000000000000503",
                Tekst = "Rani utisak mi je da ce borba trajati do poslednjih nekoliko kola.",
                DatumKreiranja = seedNow.AddDays(-3).AddHours(-22)
            }
        ];
    }

    private static IReadOnlyCollection<CommentVote> CreateCommentVotes()
    {
        return
        [
            CreateVote("665000000000000000000701", "665000000000000000000601", "665000000000000000000503", 1),
            CreateVote("665000000000000000000702", "665000000000000000000602", "665000000000000000000501", 1),
            CreateVote("665000000000000000000703", "665000000000000000000602", "665000000000000000000502", -1),
            CreateVote("665000000000000000000704", "665000000000000000000605", "665000000000000000000503", 1)
        ];
    }

    private static CommentVote CreateVote(string id, string commentId, string userId, int value)
    {
        var createdAt = new DateTime(2026, 6, 21, 11, 0, 0, DateTimeKind.Utc);
        return new CommentVote
        {
            Id = id,
            CommentId = commentId,
            UserId = userId,
            Value = value,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    private static IReadOnlyCollection<User> CreateUsers()
    {
        return
        [
            new User
            {
                Id = "665000000000000000000501",
                Username = "admin",
                Email = "admin@pleaguehub.local",
                Uloga = "administrator",
                Aktivan = true,
                DatumReg = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc),
                FavoritniTimovi = []
            },
            new User
            {
                Id = "665000000000000000000502",
                Username = "moderator",
                Email = "moderator@pleaguehub.local",
                Uloga = "moderator",
                Aktivan = true,
                DatumReg = new DateTime(2026, 8, 1, 9, 5, 0, DateTimeKind.Utc),
                FavoritniTimovi = []
            },
            new User
            {
                Id = "665000000000000000000503",
                Username = "fan",
                Email = "user@pleaguehub.local",
                Uloga = "registrovani",
                Aktivan = true,
                DatumReg = new DateTime(2026, 8, 1, 9, 10, 0, DateTimeKind.Utc),
                FavoritniTimovi =
                [
                    "665000000000000000000001",
                    "665000000000000000000002"
                ]
            }
        ];
    }

    private static IReadOnlyCollection<NewsSource> CreateNewsSources(DateTime seedNow)
    {
        return
        [
            new NewsSource
            {
                Id = "665000000000000000000801",
                Naziv = "BBC Football",
                FeedUrl = "https://feeds.bbci.co.uk/sport/football/rss.xml",
                SiteUrl = "https://www.bbc.com/sport/football",
                PodrazumevanaPouzdanost = "pouzdan_izvor",
                Aktivan = false,
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = seedNow,
                UpdatedAt = seedNow
            },
            new NewsSource
            {
                Id = "665000000000000000000802",
                Naziv = "Sky Sports Football",
                FeedUrl = "https://www.skysports.com/rss/12040",
                SiteUrl = "https://www.skysports.com/football",
                PodrazumevanaPouzdanost = "pouzdan_izvor",
                Aktivan = false,
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = seedNow,
                UpdatedAt = seedNow
            },
            new NewsSource
            {
                Id = "665000000000000000000803",
                Naziv = "Fantasy Football Scout",
                FeedUrl = "https://www.fantasyfootballscout.co.uk/feed/",
                SiteUrl = "https://www.fantasyfootballscout.co.uk",
                PodrazumevanaKategorija = "fpl",
                PodrazumevanaPouzdanost = "fpl_analiza",
                Aktivan = false,
                CreatedBy = "system",
                UpdatedBy = "system",
                CreatedAt = seedNow,
                UpdatedAt = seedNow
            }
        ];
    }
}
