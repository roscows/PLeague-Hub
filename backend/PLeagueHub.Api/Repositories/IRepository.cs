using System.Linq.Expressions;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Repositories;

public interface IRepository<TDocument>
    where TDocument : BaseDocument
{
    Task<IReadOnlyCollection<TDocument>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<TDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<TDocument?> FindOneAsync(
        Expression<Func<TDocument, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Expression<Func<TDocument, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<TDocument> CreateAsync(TDocument document, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(string id, TDocument document, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
