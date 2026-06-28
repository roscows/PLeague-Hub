namespace PLeagueHub.Api.Responses;

public sealed record ClubProfileDto(
    int ProviderId,
    string Naziv,
    string LogoUrl,
    string Stadion,
    int Osnovan,
    string Trener,
    string Drzava,
    int Pozicija,
    string Sezona,
    IReadOnlyCollection<string> Forma,
    IReadOnlyCollection<ClubMatchDto> PoslednjiMecevi,
    IReadOnlyCollection<ClubRosterDto> Roster);

public sealed record ClubMatchDto(
    string MecId,
    string Sezona,
    string Datum,
    string Protivnik,
    string ProtivnikLogo,
    bool Domaci,
    int? GolMi,
    int? GolProtivnik,
    string Ishod);

public sealed record ClubRosterDto(
    int ProviderId,
    string Ime,
    string Pozicija,
    int Broj,
    string Drzava);
