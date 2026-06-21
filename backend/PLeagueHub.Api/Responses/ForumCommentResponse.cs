namespace PLeagueHub.Api.Responses;

public sealed record ForumCommentResponse(
    string Id,
    string PostId,
    string? ParentCommentId,
    string AutorId,
    string AutorUsername,
    string AutorUloga,
    string Tekst,
    DateTime DatumKreiranja,
    bool Obrisan,
    int Broj,
    int Lajkovi,
    int Dislajkovi,
    int? TrenutniGlas);

public sealed record ForumVoteResponse(
    string CommentId,
    int Lajkovi,
    int Dislajkovi,
    int? TrenutniGlas);
