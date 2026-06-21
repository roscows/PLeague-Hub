namespace PLeagueHub.Api.Responses;

public sealed record ForumTopicResponse(
    string Id,
    string Naslov,
    string AutorId,
    string AutorUsername,
    string AutorUloga,
    int BrojOdgovora,
    DateTime DatumKreiranja,
    DateTime PoslednjaAktivnost,
    string PoslednjiAutorUsername,
    bool Istaknut);
