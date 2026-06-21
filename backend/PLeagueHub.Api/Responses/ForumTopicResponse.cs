namespace PLeagueHub.Api.Responses;

public sealed record ForumTopicResponse(
    string Id,
    string Naslov,
    string AutorId,
    string AutorUsername,
    int BrojOdgovora,
    DateTime DatumKreiranja,
    DateTime PoslednjaAktivnost,
    string PoslednjiAutorUsername,
    bool Istaknut);
