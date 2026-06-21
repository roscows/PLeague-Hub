using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public sealed class ModerationService : IModerationService
{
    private readonly IModerationRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ModerationService(IModerationRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<ModerationResult<ModerationStateResponse>> ApplyAsync(
        string targetId,
        string? actorId,
        CreateModerationActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var users = await GetActorAndTargetAsync(actorId, targetId, cancellationToken);
        if (users.Error != ModerationError.None)
            return ModerationResult<ModerationStateResponse>.Failure(users.Error, users.Message!);

        var (actor, target) = users.Value;
        if (!CanModerate(actor.Uloga, target.Uloga))
            return ModerationResult<ModerationStateResponse>.Failure(
                ModerationError.Forbidden,
                "Nemate dozvolu da moderirate ovog korisnika.");

        var type = request.Tip.Trim().ToLowerInvariant();
        if (type is not ("mute" or "suspenzija"))
            return ModerationResult<ModerationStateResponse>.Failure(
                ModerationError.Validation,
                "Tip mere mora biti mute ili suspenzija.");

        var reason = request.Razlog.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return ModerationResult<ModerationStateResponse>.Failure(
                ModerationError.Validation,
                "Razlog mere je obavezan.");

        if (!TryGetExpiry(request.Trajanje, out var expiresAt))
            return ModerationResult<ModerationStateResponse>.Failure(
                ModerationError.Validation,
                "Trajanje mere nije podrzano.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var state = new ActiveModeration
        {
            Tip = type,
            Razlog = reason,
            Pocetak = now,
            IsticeAt = expiresAt is null ? null : now + expiresAt.Value,
            ModeratorId = actor.Id!
        };

        if (!await _repository.SetActiveModerationAsync(target.Id!, state, cancellationToken))
            return ModerationResult<ModerationStateResponse>.Failure(ModerationError.NotFound, "Korisnik nije pronadjen.");

        await _repository.CreateActionAsync(CreateAudit(target.Id!, actor.Id!, "izrecena", state, now), cancellationToken);
        return ModerationResult<ModerationStateResponse>.Success(Map(state));
    }

    public async Task<ModerationResult<ModerationStateResponse>> RevokeAsync(
        string targetId,
        string? actorId,
        CancellationToken cancellationToken = default)
    {
        var users = await GetActorAndTargetAsync(actorId, targetId, cancellationToken);
        if (users.Error != ModerationError.None)
            return ModerationResult<ModerationStateResponse>.Failure(users.Error, users.Message!);

        var (actor, target) = users.Value;
        if (!CanModerate(actor.Uloga, target.Uloga))
            return ModerationResult<ModerationStateResponse>.Failure(
                ModerationError.Forbidden,
                "Nemate dozvolu da moderirate ovog korisnika.");

        var active = await ResolveActiveAsync(target, cancellationToken);
        if (active is null) return ModerationResult<ModerationStateResponse>.Success(null);

        if (!await _repository.SetActiveModerationAsync(target.Id!, null, cancellationToken))
            return ModerationResult<ModerationStateResponse>.Failure(ModerationError.NotFound, "Korisnik nije pronadjen.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await _repository.CreateActionAsync(CreateAudit(target.Id!, actor.Id!, "ukinuta", active, now), cancellationToken);
        return ModerationResult<ModerationStateResponse>.Success(null);
    }

    public async Task<ModerationAccessResult> CheckLoginAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var active = await ResolveActiveAsync(user, cancellationToken);
        return active?.Tip == "suspenzija"
            ? Denied(active)
            : new ModerationAccessResult(true, active is null ? null : Map(active), null);
    }

    public async Task<ModerationAccessResult> CheckLoginByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserAsync(userId, cancellationToken);
        return user is null
            ? new ModerationAccessResult(false, null, "Korisnik nije pronadjen.")
            : await CheckLoginAsync(user, cancellationToken);
    }

    public async Task<ModerationAccessResult> CheckForumWriteAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserAsync(userId, cancellationToken);
        if (user is null) return new ModerationAccessResult(false, null, "Korisnik nije pronadjen.");
        var active = await ResolveActiveAsync(user, cancellationToken);
        return active is null
            ? new ModerationAccessResult(true, null, null)
            : Denied(active);
    }

    public async Task<ModerationStateResponse?> GetActiveStateAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserAsync(userId, cancellationToken);
        var active = user is null ? null : await ResolveActiveAsync(user, cancellationToken);
        return active is null ? null : Map(active);
    }

    public async Task<bool> CanModerateContentAsync(
        string actorId,
        string authorId,
        CancellationToken cancellationToken = default)
    {
        var actor = await _repository.GetUserAsync(actorId, cancellationToken);
        var author = await _repository.GetUserAsync(authorId, cancellationToken);
        return actor is not null && author is not null && CanModerate(actor.Uloga, author.Uloga);
    }

    private async Task<ActiveModeration?> ResolveActiveAsync(User user, CancellationToken cancellationToken)
    {
        var active = user.AktivnaModeracija;
        if (active is null) return null;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (active.IsticeAt is null || active.IsticeAt > now) return active;

        await _repository.ExpireModerationAsync(
            user.Id!,
            active.Pocetak,
            CreateAudit(user.Id!, active.ModeratorId, "istekla", active, now),
            cancellationToken);
        user.AktivnaModeracija = null;
        return null;
    }

    private async Task<(ModerationError Error, string? Message, (User Actor, User Target) Value)> GetActorAndTargetAsync(
        string? actorId,
        string targetId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            return (ModerationError.Unauthorized, "Prijava je obavezna.", default);

        var actor = await _repository.GetUserAsync(actorId, cancellationToken);
        if (actor is null)
            return (ModerationError.Unauthorized, "Moderator nije pronadjen.", default);

        var target = await _repository.GetUserAsync(targetId, cancellationToken);
        return target is null
            ? (ModerationError.NotFound, "Korisnik nije pronadjen.", default)
            : (ModerationError.None, null, (actor, target));
    }

    private bool TryGetExpiry(string duration, out TimeSpan? expiry)
    {
        expiry = duration.Trim().ToLowerInvariant() switch
        {
            "1h" => TimeSpan.FromHours(1),
            "24h" => TimeSpan.FromHours(24),
            "7d" => TimeSpan.FromDays(7),
            "30d" => TimeSpan.FromDays(30),
            "permanent" => null,
            _ => TimeSpan.MinValue
        };
        return expiry != TimeSpan.MinValue;
    }

    private static bool CanModerate(string actorRole, string targetRole) =>
        RoleRank(actorRole) > RoleRank(targetRole) && targetRole != "administrator";

    private static int RoleRank(string role) => role switch
    {
        "administrator" => 3,
        "moderator" => 2,
        "registrovani" => 1,
        _ => 0
    };

    private static ModerationAction CreateAudit(
        string targetId,
        string moderatorId,
        string action,
        ActiveModeration state,
        DateTime now) => new()
        {
            KorisnikId = targetId,
            ModeratorId = moderatorId,
            Akcija = action,
            TipMere = state.Tip,
            Razlog = state.Razlog,
            Pocetak = state.Pocetak,
            IsticeAt = state.IsticeAt,
            Datum = now
        };

    private static ModerationStateResponse Map(ActiveModeration state) => new(
        state.Tip,
        state.Razlog,
        state.Pocetak,
        state.IsticeAt,
        state.ModeratorId);

    private static ModerationAccessResult Denied(ActiveModeration state) => new(
        false,
        Map(state),
        $"Nalog ima aktivnu meru: {state.Razlog}");
}
