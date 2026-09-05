using Microsoft.EntityFrameworkCore;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Infrastructure.Persistence;

namespace RoutineEscape.IntegrationTests.Persistence;

public sealed class DbContextTests
{
    [Fact]
    public async Task SaveChanges_PersistsUserAndTask()
    {
        var options = new DbContextOptionsBuilder<RoutineEscapeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var timestamp = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);
        var user = new AppUser(Guid.NewGuid(), 123456, "Ivan", null, "ivan", timestamp);
        var task = new TaskItem(Guid.NewGuid(), user.Id, "Send documents", timestamp);

        await using (var writeContext = new RoutineEscapeDbContext(options))
        {
            writeContext.Users.Add(user);
            writeContext.Tasks.Add(task);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new RoutineEscapeDbContext(options);
        var storedTask = await readContext.Tasks.SingleAsync();
        Assert.Equal(user.Id, storedTask.UserId);
        Assert.Equal("Send documents", storedTask.Title);
    }

    [Fact]
    public void PostgreSqlModel_ContainsAllDomainEntities()
    {
        var options = new DbContextOptionsBuilder<RoutineEscapeDbContext>()
            .UseNpgsql("Host=localhost;Database=routineescape;Username=test;Password=test")
            .Options;
        using var context = new RoutineEscapeDbContext(options);

        var entityTypes = context.Model.GetEntityTypes()
            .Select(type => type.ClrType)
            .ToHashSet();

        Assert.Contains(typeof(AppUser), entityTypes);
        Assert.Contains(typeof(TaskItem), entityTypes);
        Assert.Contains(typeof(CalendarEvent), entityTypes);
        Assert.Contains(typeof(Reminder), entityTypes);
        Assert.Contains(typeof(Note), entityTypes);
        Assert.Contains(typeof(MessageSource), entityTypes);
        Assert.Contains(typeof(Draft), entityTypes);
    }
}
