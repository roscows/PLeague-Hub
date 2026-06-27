namespace PLeagueHub.Api.Responses;

public sealed record MatchTeamDto(int ProviderId, string Naziv, string Skracenica, string LogoUrl);

public sealed record MatchHeaderDto(
    MatchTeamDto Domacin,
    MatchTeamDto Gost,
    int? GolDomacin,
    int? GolGost,
    int Kolo,
    string Sezona,
    string Status,
    DateTime Datum);

public sealed record StatItemDto(string Naziv, string Domacin, string Gost);

public sealed record IncidentDto(string Tip, int Minut, bool Domacin, string Tekst);

public sealed record LineupPlayerDto(string Ime, int Broj, bool Zamena, string Pozicija);

public sealed record LineupTeamDto(string Formacija, IReadOnlyCollection<LineupPlayerDto> Igraci);

public sealed record LineupsDto(bool Potvrdjeno, LineupTeamDto Domacin, LineupTeamDto Gost);

public sealed record MatchDetailResponse(
    MatchHeaderDto Header,
    IReadOnlyCollection<StatItemDto> Statistics,
    IReadOnlyCollection<IncidentDto> Incidents,
    LineupsDto? Lineups);
