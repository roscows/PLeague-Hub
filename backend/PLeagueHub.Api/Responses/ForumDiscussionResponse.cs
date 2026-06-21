namespace PLeagueHub.Api.Responses;

public sealed record ForumDiscussionResponse(
    string Id,
    string Naslov,
    string Sadrzaj,
    string AutorId,
    string AutorUsername,
    string AutorUloga,
    DateTime DatumKreiranja,
    bool Istaknut);
