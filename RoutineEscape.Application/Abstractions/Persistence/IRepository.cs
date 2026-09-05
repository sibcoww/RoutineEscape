using RoutineEscape.Domain.Common;

namespace RoutineEscape.Application.Abstractions.Persistence;

public interface IRepository<TEntity>
    where TEntity : class, IEntity
{
    ValueTask<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default);
    ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Remove(TEntity entity);
}
