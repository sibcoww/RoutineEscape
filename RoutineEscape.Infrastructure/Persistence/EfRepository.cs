using Microsoft.EntityFrameworkCore;
using RoutineEscape.Application.Abstractions.Persistence;
using RoutineEscape.Domain.Common;
using System.Linq.Expressions;

namespace RoutineEscape.Infrastructure.Persistence;

public sealed class EfRepository<TEntity>(RoutineEscapeDbContext dbContext) : IRepository<TEntity>
    where TEntity : class, IEntity
{
    public ValueTask<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<TEntity>().FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Set<TEntity>().AsNoTracking().ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        await dbContext.Set<TEntity>().AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

    public async ValueTask AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await dbContext.Set<TEntity>().AddAsync(entity, cancellationToken);

    public void Remove(TEntity entity) => dbContext.Set<TEntity>().Remove(entity);
}
