namespace PLeagueHub.Api.Responses;

public sealed record StandingRowResponse(
    int Position,
    int ProviderId,
    string Naziv,
    string Skracenica,
    string LogoUrl,
    int Odigrano,
    int Pobede,
    int Nereseno,
    int Porazi,
    int DatiGolovi,
    int PrimljeniGolovi,
    int GolRazlika,
    int Bodovi);
