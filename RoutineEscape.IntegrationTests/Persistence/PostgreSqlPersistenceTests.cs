using Microsoft.EntityFrameworkCore;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Infrastructure.Persistence;
using RoutineEscape.Application.Records;

namespace RoutineEscape.IntegrationTests.Persistence;

[Collection("PostgreSQL")]
public sealed class PostgreSqlPersistenceTests
{
    [PostgreSqlFact]
    public async Task MigrationAndPersistence_WorkAgainstPostgreSql()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            PostgreSqlFactAttribute.ConnectionStringVariable)!;
        PostgreSqlFactAttribute.RequireIsolatedDatabase(connectionString);
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
        var browser = new RecordOverviewService(new EfRepository<AppUser>(context), new EfRepository<TaskItem>(context),
            new EfRepository<CalendarEvent>(context), new EfRepository<Reminder>(context), new EfRepository<Note>(context));
        Assert.Equal(task.Id, Assert.Single((await browser.BrowseAsync(user.TelegramUserId, RecordView.Search, "POSTGRESQL", 0, timestamp, default)).Items).Id);
        task = storedTask; task.Complete(timestamp); await context.SaveChangesAsync();
        Assert.Equal(task.Id, Assert.Single((await browser.BrowseAsync(user.TelegramUserId, RecordView.Completed, null, 0, timestamp, default)).Items).Id);
        Assert.Empty((await browser.BrowseAsync(user.TelegramUserId, RecordView.Tasks, null, 0, timestamp, default)).Items);
        await transaction.RollbackAsync();
    }
}
