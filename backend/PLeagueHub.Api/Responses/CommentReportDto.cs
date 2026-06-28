namespace PLeagueHub.Api.Responses;

public sealed record CommentReportDto(
    string Id,
    string KomentarId,
    string PostId,
    string KomentarTekst,
    string AutorId,
    string AutorUsername,
    string PrijavioUsername,
    string Kategorija,
    string Opis,
    DateTime DatumPrijave);
