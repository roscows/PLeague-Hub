using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Responses;

namespace PLeagueHub.Api.Services;

public interface IStaffNoticeService
{
    Task<StaffNoticeDto> CreateAsync(string authorId, string tekst, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StaffNoticeDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    Task<bool> SetPinnedAsync(string id, bool pinned, CancellationToken cancellationToken);
}

public sealed class StaffNoticeService : IStaffNoticeService
{
    private readonly IRepository<StaffNoticeDocument> _notices;
    private readonly IRepository<User> _users;
    private readonly TimeProvider _timeProvider;

    public StaffNoticeService(
        IRepository<StaffNoticeDocument> notices,
        IRepository<User> users,
        TimeProvider timeProvider)
    {
        _notices = notices;
        _users = users;
        _timeProvider = timeProvider;
    }

    public async Task<StaffNoticeDto> CreateAsync(string authorId, string tekst, CancellationToken cancellationToken)
    {
        var created = await _notices.CreateAsync(
            new StaffNoticeDocument
            {
                Tekst = tekst.Trim(),
                AutorId = authorId,
                DatumKreiranja = _timeProvider.GetUtcNow().UtcDateTime
            },
            cancellationToken);

        var author = await _users.GetByIdAsync(authorId, cancellationToken);
        return ToDto(created, author?.Username);
    }

    public async Task<IReadOnlyCollection<StaffNoticeDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var users = (await _users.GetAllAsync(cancellationToken))
            .Where(user => !string.IsNullOrEmpty(user.Id))
            .GroupBy(user => user.Id!)
            .ToDictionary(group => group.Key, group => group.First());

        return (await _notices.GetAllAsync(cancellationToken))
            .OrderByDescending(notice => notice.Pinovano)
            .ThenByDescending(notice => notice.DatumKreiranja)
            .Select(notice => ToDto(notice, users.GetValueOrDefault(notice.AutorId)?.Username))
            .ToArray();
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
        => _notices.DeleteAsync(id, cancellationToken);

    public async Task<bool> SetPinnedAsync(string id, bool pinned, CancellationToken cancellationToken)
    {
        var notice = await _notices.GetByIdAsync(id, cancellationToken);

        if (notice is null)
        {
            return false;
        }

        notice.Pinovano = pinned;
        notice.PinovanoAt = pinned ? _timeProvider.GetUtcNow().UtcDateTime : null;
        return await _notices.UpdateAsync(id, notice, cancellationToken);
    }

    private static StaffNoticeDto ToDto(StaffNoticeDocument notice, string? authorUsername)
        => new(notice.Id ?? string.Empty, notice.Tekst, authorUsername ?? "Nepoznat", notice.Pinovano, notice.DatumKreiranja);
}
