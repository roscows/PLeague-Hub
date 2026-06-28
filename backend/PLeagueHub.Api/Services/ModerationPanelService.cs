using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public enum RoleChangeResult
{
    Changed,
    Forbidden,
    NotFound,
    InvalidRole,
    SelfChange,
    TargetIsAdmin
}

public interface IModerationPanelService
{
    Task<IReadOnlyCollection<ModerationActivityDto>> GetActivityAsync(int limit, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PanelUserDto>> SearchUsersAsync(string? query, bool staffOnly, CancellationToken cancellationToken);

    Task<(RoleChangeResult Result, PanelUserDto? User)> ChangeRoleAsync(
        string callerId,
        string targetId,
        string role,
        CancellationToken cancellationToken);
}

public sealed class ModerationPanelService : IModerationPanelService
{
    private const int MaxUsers = 30;
    private static readonly HashSet<string> AssignableRoles =
        new(StringComparer.Ordinal) { "registrovani", "moderator" };
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.Ordinal) { "moderator", "administrator" };

    private readonly IRepository<User> _users;
    private readonly IRepository<ModerationAction> _actions;

    public ModerationPanelService(IRepository<User> users, IRepository<ModerationAction> actions)
    {
        _users = users;
        _actions = actions;
    }

    public async Task<IReadOnlyCollection<ModerationActivityDto>> GetActivityAsync(int limit, CancellationToken cancellationToken)
    {
        var capped = Math.Clamp(limit, 1, 50);
        var users = await BuildUserMapAsync(cancellationToken);

        return (await _actions.GetAllAsync(cancellationToken))
            .OrderByDescending(action => action.Datum)
            .Take(capped)
            .Select(action => new ModerationActivityDto(
                action.Id ?? string.Empty,
                action.Akcija,
                action.TipMere,
                users.GetValueOrDefault(action.ModeratorId)?.Username ?? "Nepoznat",
                users.GetValueOrDefault(action.KorisnikId)?.Username ?? "Nepoznat",
                action.Razlog,
                action.Datum))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PanelUserDto>> SearchUsersAsync(string? query, bool staffOnly, CancellationToken cancellationToken)
    {
        var term = query?.Trim();
        var users = (await _users.GetAllAsync(cancellationToken)).AsEnumerable();

        if (staffOnly)
        {
            users = users.Where(user => StaffRoles.Contains(user.Uloga));
        }

        if (!string.IsNullOrEmpty(term))
        {
            users = users.Where(user =>
                user.Username.Contains(term, StringComparison.OrdinalIgnoreCase)
                || user.Email.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return users
            .OrderBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
            .Take(MaxUsers)
            .Select(ToDto)
            .ToArray();
    }

    public async Task<(RoleChangeResult Result, PanelUserDto? User)> ChangeRoleAsync(
        string callerId,
        string targetId,
        string role,
        CancellationToken cancellationToken)
    {
        var caller = await _users.GetByIdAsync(callerId, cancellationToken);

        if (caller is null || !string.Equals(caller.Uloga, "administrator", StringComparison.Ordinal))
        {
            return (RoleChangeResult.Forbidden, null);
        }

        if (!AssignableRoles.Contains(role))
        {
            return (RoleChangeResult.InvalidRole, null);
        }

        if (string.Equals(callerId, targetId, StringComparison.Ordinal))
        {
            return (RoleChangeResult.SelfChange, null);
        }

        var target = await _users.GetByIdAsync(targetId, cancellationToken);

        if (target is null)
        {
            return (RoleChangeResult.NotFound, null);
        }

        if (string.Equals(target.Uloga, "administrator", StringComparison.Ordinal))
        {
            return (RoleChangeResult.TargetIsAdmin, null);
        }

        target.Uloga = role;
        await _users.UpdateAsync(targetId, target, cancellationToken);
        return (RoleChangeResult.Changed, ToDto(target));
    }

    private async Task<Dictionary<string, User>> BuildUserMapAsync(CancellationToken cancellationToken)
        => (await _users.GetAllAsync(cancellationToken))
            .Where(user => !string.IsNullOrEmpty(user.Id))
            .GroupBy(user => user.Id!)
            .ToDictionary(group => group.Key, group => group.First());

    private static PanelUserDto ToDto(User user)
        => new(
            user.Id ?? string.Empty,
            user.Username,
            user.Email,
            user.Uloga,
            user.Aktivan,
            user.AktivnaModeracija?.Tip);
}
