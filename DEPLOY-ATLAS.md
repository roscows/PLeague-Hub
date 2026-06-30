# Prezentacija sa drugog računara — MongoDB Atlas + lokalni run

Cilj: ista baza dostupna sa bilo kog računara, a aplikacija se pokreće lokalno na
tuđem laptopu.

## Šta je već rešeno
- ✅ **Slike** (grbovi + fotke igrača) su sada u repou — stižu sa `git clone`.
- ✅ **JWT secret** je u `appsettings.json` (dev default) — radi bez dodatne konfiguracije.
- ✅ **FootApi ključ NIJE potreban** za demo (svi podaci su u bazi). *Napomena: ne otvaraj
  profil igrača/kluba koji nikad nije otvoren (nije keširan) jer bi to pokušalo živi poziv.
  Sve što ćeš pokazivati je već keširano.*

## Korak 1 — Napravi besplatan MongoDB Atlas klaster (jednom)
1. Idi na **https://www.mongodb.com/cloud/atlas/register** i napravi nalog.
2. Napravi **besplatan M0** klaster (provider/region nije bitan; uzmi najbliži).
3. **Database Access** → *Add New Database User*: korisničko ime + lozinka
   (zapamti ih; bez specijalnih znakova radi lakšeg URL-a, npr. `pleague` / `Lozinka123`).
4. **Network Access** → *Add IP Address* → **Allow access from anywhere** (`0.0.0.0/0`)
   — VAŽNO da bi radilo sa bilo kog računara.
5. **Connect** → *Drivers* → kopiraj **connection string**, izgleda ovako:
   ```
   mongodb+srv://pleague:Lozinka123@cluster0.xxxxx.mongodb.net/?retryWrites=true&w=majority
   ```

## Korak 2 — Prebaci podatke u Atlas (ja to mogu odraditi)
Pošalji mi taj connection string i ja ću pokrenuti migraciju (dump lokalne baze →
restore u Atlas). Komanda koja se izvršava (za referencu):
```bash
docker exec pleaguehub-mongodb mongodump --db=PLeagueHub --archive=/tmp/pl.gz --gzip
docker exec pleaguehub-mongodb mongorestore --uri="<ATLAS_URI>" --gzip --archive=/tmp/pl.gz
```
Posle ovoga, Atlas ima identičnu bazu `PLeagueHub` (svih 17 kolekcija).

## Korak 3 — Poveži aplikaciju na Atlas
Dva načina (izaberi jedan):

**A) Najjednostavnije za demo** — upiši Atlas URI u `appsettings.json`
(`backend/PLeagueHub.Api/appsettings.json`), polje `MongoDb.ConnectionString`:
```json
"MongoDb": { "ConnectionString": "mongodb+srv://pleague:Lozinka123@cluster0.xxxxx.mongodb.net/", ... }
```
Tada svaki `git clone` odmah radi. *(Mana: lozinka je u kodu — promeni je u Atlas-u
posle prezentacije.)*

**B) Čistije** — ne diraj `appsettings.json`, nego postavi env var pre pokretanja:
```powershell
$env:MongoDb__ConnectionString = "mongodb+srv://pleague:Lozinka123@cluster0.xxxxx.mongodb.net/"
dotnet run
```

## Korak 4 — Na drugarovom laptopu (demo dan)
1. Instaliraj **.NET 10 SDK**: https://dotnet.microsoft.com/download/dotnet/10.0
2. Instaliraj **Node.js (LTS)**: https://nodejs.org
3. `git clone <repo-url>` pa uđi u folder.
4. **Backend:**
   ```powershell
   cd backend/PLeagueHub.Api
   # ako koristiš env var (način B):
   $env:MongoDb__ConnectionString = "mongodb+srv://..."
   dotnet run
   ```
   API sluša na `http://localhost:5000`.
5. **Frontend** (novi terminal):
   ```powershell
   cd frontend
   npm install
   npm run dev
   ```
   Otvori `http://localhost:3000`.
6. Uloguj se: `admin` / `moderator`, lozinka `PLeague123!`.

## Provera pre prezentacije
- Učitaj početnu — vide se podaci (znači Atlas konekcija radi).
- Otvori tabelu, profil kluba, statistiku, forum, moderatorski panel.
- Ako nešto ne radi: proveri Network Access u Atlas-u (0.0.0.0/0) i tačnost URI-ja.

## Bezbednosna napomena
- Po završetku prezentacije, u Atlas-u promeni lozinku DB korisnika (ako si je upisao u
  `appsettings.json`) i po želji ograniči Network Access.
