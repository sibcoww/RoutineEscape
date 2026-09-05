using Microsoft.EntityFrameworkCore;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Infrastructure.Persistence;

namespace RoutineEscape.IntegrationTests.Persistence;

public sealed class PostgreSqlPersistenceTests
{
    [PostgreSqlFact]
    public async Task MigrationAndPersistence_WorkAgainstPostgreSql()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            PostgreSqlFactAttribute.ConnectionStringVariable)!;
        var options = new DbContextOptionsBuilder<RoutineEscapeDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var context = new RoutineEscapeDbContext(options);
        await context.Database.MigrateAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new AppUser(Guid.NewGuid(), Random.Shared.NextInt64(1, long.MaxValue),
            "Integration", "Test", null, timestamp);
        var task = new TaskItem(Guid.NewGuid(), user.Id, "PostgreSQL persistence check", timestamp);

        context.Users.Add(user);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var storedTask = await context.Tasks.SingleAsync(candidate => candidate.Id == task.Id);
        Assert.Equal(user.Id, storedTask.UserId);
        await transaction.RollbackAsync();
    }
}
