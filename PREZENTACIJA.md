# PLeague Hub — Prezentacija projekta

> Vodič za usmenu prezentaciju (20–30 min). Sadrži sve: tehnologije, arhitekturu,
> implementaciju svakog modula, bazu, eksterni API, strukture i obrasce, testiranje,
> bezbednost, razvojni proces, demo scenario i anticipirana pitanja.

---

## 0. Sažetak u jednoj rečenici

**PLeague Hub** je full-stack web portal o engleskoj Premijer ligi: prati rezultate,
tabelu, statistiku igrača, profile klubova i igrača, vesti i forum sa zajednicom,
uz kompletan sistem moderacije. Podaci se preuzimaju sa eksternog sportskog API-ja i
**trajno čuvaju u bazi**, pa je aplikacija u svakom trenutku demonstrabilna i bez
interneta.

---

## 1. Cilj i koncept

- **Domen:** sportski informativni portal (Premier League) + društvena komponenta.
- **Ključna ideja:** spojiti **žive sportske podatke** (rezultati, tabela, strelci,
  sastavi) sa **korisnički generisanim sadržajem** (forum, komentari, glasanje) i
  **uredničkim sadržajem** (vesti), uz **moderaciju** kao vezivno tkivo.
- **Princip „sve u bazi":** svaki podatak preuzet sa eksternog API-ja se denormalizuje
  i upisuje u MongoDB. Razlog: eksterni API ima dnevnu kvotu, pa se oslanjanjem na bazu
  garantuje da prezentacija/demo rade pouzdano i offline.

---

## 2. Tehnološki stek (sa verzijama)

### Backend — REST API
| Tehnologija | Verzija | Uloga |
|---|---|---|
| .NET / ASP.NET Core | **net10.0** | Web API, DI kontejner, middleware pipeline |
| C# | 13 (net10) | Jezik; `record` tipovi, pattern matching, nullable refs |
| MongoDB.Driver | 3.9.0 | Pristup NoSQL bazi |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 | JWT autentifikacija |
| `PasswordHasher<User>` (ASP.NET Core Identity) | ugrađeno | Heš lozinki (PBKDF2-HMAC-SHA256, salt po korisniku) |
| Swashbuckle.AspNetCore (Swagger) | 10.2.1 | Auto-dokumentacija i testiranje API-ja |
| System.ServiceModel.Syndication | 10.0.9 | Parsiranje RSS/Atom feedova (vesti) |
| HtmlSanitizer | 9.0.892 | Čišćenje HTML-a iz spoljnih izvora (XSS zaštita) |
| xUnit + Microsoft.AspNetCore.Mvc.Testing | — | Unit/integration testovi (**280 testova**) |

### Frontend — Single Page Application
| Tehnologija | Verzija | Uloga |
|---|---|---|
| React | **19** | UI biblioteka (funkcionalne komponente + hooks) |
| TypeScript | 5.7 | Statički tipovi kroz ceo frontend |
| Vite | 6 | Build alat i dev server (HMR) |
| react-router-dom | 7.1 | Klijentsko rutiranje (SPA) |
| axios | 1.7 | HTTP klijent + interceptori (token, 401 handling) |
| Tailwind CSS | 3.4 | Utility-first stilizovanje |
| lucide-react | — | Ikonice |
| Vitest + Testing Library + jsdom | — | Frontend testovi (**93 testa**) |

### Baza i infrastruktura
- **MongoDB** (dokument-orijentisana NoSQL baza), pokrenut kroz **Docker** kontejner.
- **Git** za verzionisanje; razvoj po grani (`feature/...`).
- **.NET User Secrets** za tajne (API ključ, JWT secret) — nikad u repou.

---

## 3. Arhitektura sistema

Klasična **troslojna client–server** arhitektura sa jasnom separacijom odgovornosti:

```
┌─────────────────────────────┐        HTTPS / JSON        ┌──────────────────────────────┐
│   FRONTEND (React SPA)       │  ───────────────────────▶ │   BACKEND (ASP.NET Core API)  │
│   - Pages / Components       │  ◀─────────────────────── │   Controllers → Services →     │
│   - Context (auth)           │                            │   Repositories                 │
│   - axios klijenti           │                            └───────────────┬────────────────┘
└─────────────────────────────┘                                            │
                                                            ┌───────────────┴────────────────┐
                                                            │   MongoDB (17 kolekcija)        │
                                                            └─────────────────────────────────┘
                                                                            ▲
                                                            ┌───────────────┴────────────────┐
                                                            │   FootApi (SofaScore mirror)    │
                                                            │   eksterni sportski API         │
                                                            └─────────────────────────────────┘
```

**Slojevi na backendu:**
1. **Controllers (16)** — tanak HTTP sloj: rutiranje, autorizacija, mapiranje rezultata
   u HTTP statuse. Bez poslovne logike.
2. **Services (~60 fajlova)** — poslovna logika: validacija, orkestracija, mapiranje u
   DTO. Npr. `MatchDetailService`, `ClubProfileService`, `CommentReportService`,
   `ModerationPanelService`.
3. **Repositories (8)** — pristup podacima preko generičkog `IRepository<T>` i
   specijalizovanih repozitorijuma (`IForumRepository`, `IModerationRepository`).
4. **Data** — `MongoContext` (kolekcije), `MongoIndexInitializer` (indeksi na startu),
   `DatabaseSeeder`.

Sve zavisnosti se ubacuju kroz **Dependency Injection** (interfejsi → implementacije),
što omogućava lako testiranje (fake repozitorijumi).

---

## 4. Struktura projekta

### Backend (`backend/PLeagueHub.Api`)
```
Controllers/      16  - HTTP endpointi
Services/         18  - poslovna logika (auth, forum, moderacija, vesti...)
  Football/       31  - integracija sa FootApi + sportski servisi
  News/           12  - agregacija vesti (RSS), uvoz, sanitizacija
Models/           19  - dokument-modeli (mapiraju se na Mongo kolekcije)
Repositories/      8  - generički + specijalizovani repozitorijumi
Data/              4  - MongoContext, indeksi, seeding
  Seeding/         1
Configuration/     4  - settings klase (Mongo, JWT, FootApi)
Requests/         22  - DTO za ulaz (telo zahteva)
Responses/        24  - DTO za izlaz (odgovori)
Program.cs            - kompozicioni koren (DI, middleware pipeline)
```

### Frontend (`frontend/src`)
```
pages/         15  - rutirane stranice (Home, Results, Table, Stats, Forum,
                     PlayerProfile, ClubProfile, ModerationPanel, News, ...)
components/         - deljive komponente (Layout, GlobalSearch, MatchRow,
                     TeamLogo, forum/*, moderation/*)
services/          - axios API klijenti + pomoćne funkcije (1 po domenu)
contexts/          - React Context (AuthContext: globalno stanje korisnika)
routes/            - ProtectedRoute (zaštita po ulozi)
types/api.ts       - svi TypeScript tipovi (ogledalo backend DTO-a)
utils/             - npr. forumTree (gradnja stabla komentara), relativeTime
```

### Dokumentacija razvoja
```
docs/superpowers/specs/   - dizajn specifikacije po feature-u
docs/superpowers/plans/   - implementacioni planovi (TDD korak-po-korak)
```

---

## 5. Baza podataka — MongoDB

### Zašto NoSQL/MongoDB
- Podaci su prirodno **dokument-orijentisani** (meč, profil, vest = JSON dokument).
- **Denormalizacija** je poželjna za brzo čitanje (npr. u statistici igrača čuvamo i
  naziv/grb tima da bismo izbegli spajanja).
- Fleksibilna šema olakšava postepeni razvoj (dodavanje polja bez migracija).

### Kolekcije (17) i približan broj dokumenata
| Kolekcija | ~Dok. | Sadržaj |
|---|---|---|
| Matches | 1232 | utakmice (4 sezone) |
| Teams | 27 | klubovi + grbovi |
| PlayerSeasonStats | 237 | strelci/asistenti po sezoni |
| MatchDetails | 10 | keširani detalji meča (statistika/incidenti/sastavi) |
| PlayerProfiles | 8 | bio igrača (lenjo keširano) |
| ClubProfiles | 4 | detalji + roster kluba (lenjo keširano) |
| Posts | 53 | forum teme + vesti (deljeni entitet) |
| Comments | 15 | komentari (ugnježdeni) |
| CommentVotes | 5 | glasovi na komentare |
| CommentReports | 2 | prijave komentara |
| ModerationActions | 6 | istorija kazni (mute/suspenzija) |
| StaffNotices | 0 | obaveštenja za moderatorski tim |
| NewsSources | 3 | RSS izvori vesti |
| EditorialAuditEvents | 48 | revizioni log uredničkih akcija |
| Users | 6 | korisnici |
| Players, Statistics | 6/3 | (legacy/seed entiteti) |

### Indeksi
`MongoIndexInitializer` kreira indekse na startu aplikacije — npr.
`idx_matches_sezona_kolo`, `idx_teams_provider_id` (unique, sparse),
`idx_playerSeasonStats_sezona_golovi`, `idx_commentReports_status_datum`,
`uq_users_email`/`uq_users_username` (unique). Indeksi prate najčešće upite
(sortiranje tabele/strelaca, dedup korisnika, red prijava).

### Mapiranje modela
Svaki model nasleđuje `BaseDocument` (Mongo `_id` kao string). Polja se mapiraju
atributima `[BsonElement("...")]`; reference na druge dokumente koriste
`[BsonRepresentation(BsonType.ObjectId)]`.

---

## 6. Eksterni API — FootApi (SofaScore mirror)

- **Provajder:** `footapi7.p.rapidapi.com` (preko RapidApi), ogledalo SofaScore podataka.
- **Plan:** BASIC — ima **ograničenje po sekundi** (HTTP 429) i **dnevnu kvotu**.
- **Ključ:** čuva se u **.NET User Secrets** (nikad u kodu/repou).
- **Apstrakcija:** interfejs `IFootballProvider` + implementacija `FootApiClient`.
  Time je eksterni servis izolovan i lako se mock-uje u testovima.
- **Kontrola brzine:** `IProviderRequestPacer` (pacing) + retry sa backoff-om na 429.

### Korišćeni endpointi
| Svrha | Putanja (sažeto) |
|---|---|
| Tabela | `/tournament/{t}/season/{s}/standings/total` |
| Sezone | `/tournament/{t}/seasons` |
| Utakmice | `/tournament/{t}/season/{s}/matches/last|next/{page}` |
| Detalji meča | `/match/{id}/statistics|incidents|lineups` |
| Najbolji strelci/asistenti | `/tournament/{t}/season/{s}/best-players` |
| Profil igrača + foto | `/player/{id}` , `/player/{id}/image` |
| Detalji + roster kluba | `/team/{id}` , `/team/{id}/players` |
| Grb tima | `/team/{id}/image` |

> Detalj: za tabelu/sezone/strelce/profile koristi se `uniqueTournament` id **17**, a za
> utakmice id sezona-turnir **1**. Ovo je otkriveno empirijski (provera „shape"-a odgovora).

### Strategija: „sve u bazi" + lenjo keširanje (persist-on-first-view)
- **Bulk uvoz** (admin): utakmice, timovi, grbovi, statistika strelaca — povuku se jednom
  i upišu u Mongo.
- **Lenjo keširanje:** detalji meča, profili igrača/klubova se povlače **pri prvom
  otvaranju** i trajno upisuju (isti obrazac kao `MatchDetailService`). Svako sledeće
  otvaranje čita iz baze — bez ijednog poziva ka API-ju. Time se štedi kvota, a podaci
  ostaju trajni.

---

## 7. Autentifikacija i autorizacija

- **Registracija/Login** → izdaje se **JWT** (`Bearer`) token sa claim-ovima (id, uloga).
- **Lozinke:** `PasswordHasher<User>` (PBKDF2 + salt po korisniku) — ne čuva se plaintext.
- **Autorizacija po ulogama** preko `[Authorize(Roles="...")]` na endpointima.
- **Frontend:** `AuthContext` drži korisnika i token; axios interceptor dodaje token na
  svaki zahtev i hvata 401 (automatska odjava). `ProtectedRoute` štiti rute po ulozi.
- **Moderaciona stanja:** korisnik može imati aktivan **mute/suspenziju**; login vraća
  razlog ako je naloga ograničen.

### Četiri tipa korisnika
| Uloga | Može |
|---|---|
| **Gost** | čita javni sadržaj (rezultati, tabela, statistika, profili, vesti, forum read-only) |
| **Registrovani** | + piše forum diskusije/komentare, glasa, bira omiljeni klub, prijavljuje komentare |
| **Moderator** | + objavljuje/uređuje vesti i izvore, moderiše korisnike, briše/ističe komentare, rešava prijave, piše obaveštenja timu |
| **Administrator** | + CRUD mečeva/timova/igrača/statistika, FootApi integracije, **promena uloga** korisnika |

---

## 8. Funkcionalnosti (mapirano na esencijalne)

### CRUD operacije
- **Vesti** (kreiranje/izmena/brisanje/pregled), **Izvori vesti** (pun CRUD),
  **Mečevi**, **Statistike**, **Timovi/igrači** — admin/moderator.
- **Obaveštenja za tim** (StaffNotices) — kreiranje/brisanje/pin.

### Interakcije više tipova korisnika
- **Forum diskusije + komentari** (registrovani ↔ registrovani ↔ gost čita).
- **Glasanje na komentare** (like/dislike na tuđe).
- **Moderacija korisnika** (moderator → registrovani: mute/suspenzija).
- **Isticanje/brisanje komentara** (moderator nad korisničkim sadržajem).
- **Prijava komentara → rešavanje** (registrovani prijavi → moderator postupi).
- **Promena uloge** (administrator → korisnik: promocija/degradacija).

---

## 9. Detaljan pregled modula

### 9.1 Rezultati, Tabela, Statistika
- **Rezultati:** utakmice po sezoni i kolu (GW), filteri, klik na meč otvara detalje.
- **Tabela:** **ne čuva se kao gotov podatak** — `StandingsService` je **računa iz
  sačuvanih utakmica** (pobede/nerešeno/porazi, gol-razlika, bodovi), pa je uvek
  konzistentna sa rezultatima. Podržava istorijske sezone.
- **Detalji meča** (`/mec/:id`): statistika (posed, šutevi…), tok meča (golovi/kartoni),
  postave — lenjo keširano u Mongo pri prvom otvaranju.
- **Statistika:** najbolji strelci i asistenti po sezoni (spojeni iz dve liste API-ja),
  sortiranje Gol/Ast, pretraga, klik na igrača/tim.

### 9.2 Profili igrača i klubova (FlashScore-stil)
- **Igrač** (`/igrac/:id`): foto, pozicija, godine, nacionalnost, visina, klub +
  statistika po sezonama.
- **Klub** (`/klub/:id`): stadion, trener, godina osnivanja, država; pozicija na tabeli i
  forma (poslednjih 5) i poslednji mečevi **iz baze**; ceo roster sa linkovima na igrače.
- **Lenjo keširanje:** prvo otvaranje povuče sa FootApi i upiše u Mongo (foto se kešira
  lokalno u `wwwroot/player-photos`), sledeća otvaranja čitaju iz baze.
- **Klikabilnost svuda:** Statistika → igrač/klub; Tabela → klub; Detalji meča → klub;
  Profil kluba → poslednji mečevi vode na detalj meča; roster → profil igrača.

### 9.3 Globalna pretraga
- Pretraga gore u headeru po **klubovima i igračima** (iz `Teams` i `PlayerSeasonStats`),
  podstring case-insensitive, rezultat vodi direktno na profil (`/klub/:id`, `/igrac/:id`).
- Debounce + otkazivanje zastarelih zahteva (request id).

### 9.4 Forum
- **Diskusije i ugnježdeni komentari** (stablo neograničene dubine).
- **Glasanje** like/dislike (ne na sopstveni komentar).
- **Prijava komentara** (kategorija + opis) → ide u moderatorski red.
- **Soft-delete** (komentar/tema se označi obrisanim, ne briše fizički).

### 9.5 Vesti (uredništvo + agregacija)
- **Ručno** objavljivanje vesti (moderator/admin) i **X (Twitter) embed**.
- **Automatska agregacija** sa RSS izvora (`System.ServiceModel.Syndication`):
  `NewsIngestionService` + pozadinski `NewsIngestionWorker` (po konfiguraciji).
- **HtmlSanitizer** čisti spoljni HTML (XSS), dedup preko fingerprint-a,
  **EditorialAuditEvents** beleži uredničke akcije (revizioni log).
- Komentarisanje vesti (isti komentar-sistem kao forum).

### 9.6 Moderacija (vezivno tkivo)
- **Kazne korisnicima:** mute/suspenzija sa trajanjem i razlogom; istorija u
  `ModerationActions`; korisnik vidi razlog pri loginu.
- **Moderatorski panel** (`/moderacija`, samo mod/admin) — tri sekcije:
  1. **Obaveštenja za tim** — pinabilna obaveštenja/uputstva kolegama + **feed nedavne
     aktivnosti** (ko je koga mutovao/suspendovao).
  2. **Prijave komentara** — red prijava; akcije: obriši komentar / odbaci / moderiši autora.
  3. **Korisnici** — pretraga svih korisnika + lista admina/moderatora; moderacija;
     **admin menja uloge** (registrovani ↔ moderator) sa zaštitama (ne sopstvenu, ne
     drugog admina).

---

## 10. Ključni dizajn-obrasci i strukture

| Obrazac / struktura | Gde i zašto |
|---|---|
| **Repository pattern** | generički `IRepository<T>` + specijalizovani; izoluje pristup bazi, lako mockovanje |
| **Service layer** | sva poslovna logika van kontrolera; testabilno |
| **DTO (records)** | `Requests/*` i `Responses/*` razdvajaju domen-modele od API ugovora |
| **Result/enum obrazac** | npr. `ReportCreateResult`, `RoleChangeResult` — eksplicitni ishodi umesto izuzetaka za očekivane greške |
| **Persist-on-first-view (lenjo keširanje)** | detalji meča, profili — štednja kvote + offline |
| **Denormalizacija** | statistika igrača nosi naziv/grb tima; brzo čitanje bez spajanja |
| **Derived data** | tabela se računa iz mečeva (jedan izvor istine) |
| **Strategy/adapter** | `IFootballProvider` apstrahuje eksterni API |
| **Pacing + retry/backoff** | poštovanje rate-limita (429) |
| **In-memory cache** (`IMemoryCache`) | keširanje čestih čitanja |
| **Soft-delete** | sadržaj se označava obrisanim (revizija, oporavak) |
| **Stablo komentara** | rekurzivna gradnja (`forumTree`); klijent koristi `Map` za O(1) pristup |
| **Background worker** | `NewsIngestionWorker` (HostedService) za periodičnu agregaciju |
| **Open-generic DI** | `IRepository<>` se razrešava za bilo koji `BaseDocument` |

---

## 11. Frontend arhitektura

- **SPA** sa klijentskim rutiranjem (`react-router-dom`): rute kao `/`, `/results`,
  `/tabela`, `/stats`, `/mec/:id`, `/igrac/:id`, `/klub/:id`, `/forum`, `/news`,
  `/moderacija` (zaštićeno).
- **Globalno stanje** kroz `AuthContext` (korisnik, token, `hasRole`, `refreshProfile`).
- **API sloj:** po jedan axios klijent po domenu (`matchesApi`, `standingsApi`,
  `playerStatsApi`, `clubsApi`, `playersApi`, `reportsApi`, `panelApi`, …) — centralizovan
  `api` sa interceptorima.
- **Tipovi:** `types/api.ts` je TypeScript ogledalo backend DTO-a (camelCase).
- **Stilizovanje:** Tailwind utility klase; responzivno (mobilna + desktop navigacija).
- **Komponente:** kompozicija (npr. `ForumThread → ForumComment`, `ModerationModal`,
  `TeamLogo` sa fallback-om).

---

## 12. Testiranje (TDD pristup)

- **Backend: 280 xUnit testova** — servisi (sa fake repozitorijumima), kontroleri
  (sa lažnim servisima i postavljenim `ClaimsPrincipal`-om), provajder (parsiranje
  reprezentativnog JSON-a uz `HttpMessageHandler` stub).
- **Frontend: 93 Vitest testa** — komponente/stranice (Testing Library + jsdom),
  API klijenti (mock `api`).
- **Metodologija:** za svaki feature — *spec → plan → test → implementacija → verifikacija*.
  Testovi se pišu pre koda gde ima smisla; verifikacija pre tvrdnje da nešto radi.
- **Izolacija eksternih zavisnosti:** baza i FootApi se ne diraju u unit testovima
  (fake/stub), pa su testovi brzi i deterministički.

---

## 13. Bezbednost

- **JWT** sa proverom potpisa; tajne u User Secrets.
- **Hešovane lozinke** (PBKDF2 + salt).
- **Autorizacija po ulogama** na svakom osetljivom endpointu.
- **HtmlSanitizer** nad spoljnim sadržajem (vesti) — anti-XSS.
- **Validacija ulaza** (npr. dužina query-ja u pretrazi, kategorije prijave, role).
- **Zaštite poslovne logike** (ne prijavljuj sopstveni komentar, ne menjaj sopstvenu
  ulogu, ne diraj drugog admina, jedna prijava po korisniku po komentaru).
- **CORS** ograničen na frontend origin (port 3000).
- **Soft-delete** — sadržaj ostaje za reviziju.

---

## 14. Razvojni proces i alati

- **Metodologija:** spec-driven + TDD; svaki feature ima dizajn spec i implementacioni
  plan u `docs/superpowers/`.
- **Git:** rad po feature grani; mali, jasni commit-i (jedan po koraku); poruke na
  engleskom.
- **Okruženje:** Windows + .NET 10, MongoDB u Docker-u, Vite dev server.
- **Swagger** za ručno testiranje API-ja tokom razvoja.

---

## 15. Izazovi i rešenja

| Izazov | Rešenje |
|---|---|
| Dnevna kvota eksternog API-ja | „Sve u bazi" + lenjo keširanje (persist-on-first-view) |
| Rate-limit (429) | Pacer + retry sa backoff-om |
| Konzistentnost tabele i rezultata | Tabela se **računa** iz mečeva (jedan izvor istine) |
| Nepoznat „shape" odgovora API-ja | Empirijska provera pre oslanjanja (probni pozivi) |
| Grbovi ispalih klubova | Sinhronizacija grbova za sve timove iz baze |
| Skaliranje sadržaja (vesti) | RSS agregacija + dedup + sanitizacija |

---

## 16. Moguća proširenja

- Prognoze rezultata + rang lista (gejmifikacija).
- Ocene igrača posle meča.
- Obaveštenja korisnicima (read/unread).
- Paginacija foruma/vesti, pretraga unutar foruma.
- Push/email notifikacije, PWA.

---

## 17. Predlog scenarija za demo (uživo, ~7–8 min)

1. **Početna:** poslednje kolo + mini tabela + top strelci + vesti (sve pravi podaci).
2. **Tabela → istorijska sezona;** klik na klub → **profil kluba** (stadion, trener,
   forma, roster); klik na meč u „Poslednji mečevi" → **detalji meča** (statistika,
   tok, postave).
3. **Statistika → klik na igrača → profil igrača** (bio + sezone).
4. **Pretraga gore:** ukucati igrača i klub → direktno na profile.
5. **Forum:** napisati komentar (kao registrovani), glasati, **prijaviti** tuđi komentar.
6. **Login kao admin → „Moderacija":** obaveštenje za tim (pin), red prijava (obriši/odbaci),
   pretraga korisnika, **promocija u moderatora**; pokazati da gost ne vidi panel.
7. (Opc.) **Swagger** — pokazati API i JWT.

---

## 18. Anticipirana pitanja profesora (i odgovori)

- **Zašto MongoDB a ne relaciona baza?** Podaci su dokument-orijentisani, denormalizacija
  ubrzava čitanje, fleksibilna šema olakšava iterativni razvoj. Konzistentnost gde je
  bitna (npr. tabela) postiže se izvođenjem iz jednog izvora.
- **Kako čuvate lozinke?** `PasswordHasher` (PBKDF2 + salt po korisniku), nikad plaintext.
- **Kako sprečavate XSS u vestima?** HtmlSanitizer čisti spoljni HTML pre prikaza.
- **Šta ako padne eksterni API?** Aplikacija radi iz baze (sve je keširano); novi podaci
  se mogu povući kasnije.
- **Kako se tabela slaže sa rezultatima?** Tabela se ne čuva — računa se iz istih utakvica.
- **Kako testirate bez baze i bez API-ja?** Fake repozitorijumi i HTTP stubovi; 280+93
  testa.
- **Kako je rešena autorizacija?** JWT + role na endpointima + `ProtectedRoute` na frontu.
- **Kako su modelovane interakcije korisnika?** Forum, glasanje, prijave, moderacija,
  promena uloga — svaka uključuje 2+ tipa korisnika.

---

## 19. Brojke za kraj (impresija)

- **Backend:** 16 kontrolera, ~60 servisa, 19 modela, 8 repozitorijuma, 280 testova.
- **Frontend:** 15 stranica, desetine komponenti, 93 testa.
- **Baza:** 17 kolekcija; **1232 utakmice**, 27 klubova, 237 sezonskih statistika.
- **4 tipa korisnika**, **11+ esencijalnih funkcionalnosti** (5 CRUD + 6 interakcija).

---

## 20. Predlog rasporeda vremena (25 min)

| Min | Tema |
|---|---|
| 0–2 | Uvod, cilj, koncept „sve u bazi" |
| 2–6 | Tehnološki stek + arhitektura (dijagram) |
| 6–9 | Baza (MongoDB, kolekcije, indeksi) + eksterni API |
| 9–12 | Auth, uloge, bezbednost |
| 12–20 | **Demo uživo** (scenario iz tačke 17) |
| 20–23 | Dizajn-obrasci, strukture, testiranje |
| 23–25 | Izazovi/rešenja, proširenja, pitanja |

---

*Napomena: pokreni MongoDB (Docker) i .NET API (port 5000) pre demoa; frontend na
portu 3000. Nalozi: `admin` / `moderator`, lozinka `PLeague123!`.*
