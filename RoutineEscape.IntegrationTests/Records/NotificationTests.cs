using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RoutineEscape.Application.Records;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;
using RoutineEscape.Infrastructure.Persistence;
using RoutineEscape.IntegrationTests.Persistence;

namespace RoutineEscape.IntegrationTests.Records;

[Collection("PostgreSQL")]
public sealed class NotificationTests
{
    private static readonly DateTimeOffset Due = DateTimeOffset.Parse("2035-09-16T13:00:00Z");
    private static DbContextOptions<RoutineEscapeDbContext> Options() => new DbContextOptionsBuilder<RoutineEscapeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    [Fact]
    public async Task FirstDeliveryAndOnlyOneFollowUp_SurviveNewContexts()
    {
        var options = Options(); var sender = new Sender();
        await using (var db = new RoutineEscapeDbContext(options))
        {
            await Seed(db, Intent.Reminder);
            Assert.False(await Processor(db, sender).ProcessOneAsync(Due.AddSeconds(-1), default));
            Assert.True(await Processor(db, sender).ProcessOneAsync(Due, default));
        }
        await using (var db = new RoutineEscapeDbContext(options))
        {
            Assert.False(await Processor(db, sender).ProcessOneAsync(Due.AddMinutes(9), default));
            Assert.True(await Processor(db, sender).ProcessOneAsync(Due.AddMinutes(10), default));
            Assert.False(await Processor(db, sender).ProcessOneAsync(Due.AddMinutes(20), default));
            Assert.Equal(2, (await db.NotificationSchedules.SingleAsync()).Stage);
        }
        Assert.Equal(new[] { false, true }, sender.Messages.Select(item => item.IsFollowUp));
    }

    [Theory]
    [InlineData(Intent.Task)]
    [InlineData(Intent.Reminder)]
    [InlineData(Intent.Event)]
    public async Task SnoozeStartsNewCycle_DoneCancelsIt_AndForeignOrStaleActionsCannotChangeIt(Intent intent)
    {
        await using var db = new RoutineEscapeDbContext(Options()); await Seed(db, intent);
        var sender = new Sender(); var processor = Processor(db, sender);
        await processor.ProcessOneAsync(Due, default);
        var token = sender.Messages.Single().ActionToken;
        Assert.Null(await processor.ActAsync(456, token, "done", Due, default));
        Assert.NotNull(await processor.ActAsync(123, token, "10", Due.AddMinutes(2), default));
        if (intent == Intent.Event) Assert.Equal(Due, (await db.Events.SingleAsync()).StartUtc);
        Assert.Null(await processor.ActAsync(123, token, "60", Due.AddMinutes(2), default));
        Assert.False(await processor.ProcessOneAsync(Due.AddMinutes(10), default));
        Assert.True(await processor.ProcessOneAsync(Due.AddMinutes(12), default));
        Assert.False(sender.Messages.Last().IsFollowUp);
        var nextToken = sender.Messages.Last().ActionToken;
        Assert.NotEqual(token, nextToken);
        Assert.NotNull(await processor.ActAsync(123, nextToken, "done", Due.AddMinutes(13), default));
        Assert.Null(await processor.ActAsync(123, nextToken, "done", Due.AddMinutes(13), default));
        Assert.False(await processor.ProcessOneAsync(Due.AddMinutes(22), default));
        if (intent == Intent.Task) Assert.Equal(TaskItemStatus.Completed, (await db.Tasks.SingleAsync()).Status);
        if (intent == Intent.Reminder) Assert.Equal(ReminderStatus.Triggered, (await db.Reminders.SingleAsync()).Status);
        if (intent == Intent.Event) { Assert.Single(db.Events); Assert.Equal(3, (await db.NotificationSchedules.SingleAsync()).Stage); }
    }

    [Fact]
    public async Task SendFailureRetriesWithoutAdvancingStage_AndSuccessSchedulesFollowUpFromActualDelivery()
    {
        await using var db = new RoutineEscapeDbContext(Options()); await Seed(db, Intent.Reminder);
        var sender = new Sender { Fail = true }; var processor = Processor(db, sender);
        Assert.True(await processor.ProcessOneAsync(Due, default));
        var job = await db.NotificationSchedules.SingleAsync();
        Assert.Equal(0, job.Stage); Assert.Equal(1, job.Attempts);
        Assert.False(await processor.ProcessOneAsync(Due.AddSeconds(29), default));
        sender.Fail = false;
        Assert.True(await processor.ProcessOneAsync(Due.AddSeconds(30), default));
        Assert.Equal(Due.AddMinutes(10).AddSeconds(30), job.NextAttemptAt);
        Assert.Single(sender.Messages);
    }

    [Fact]
    public async Task DateOnlyTaskNeverSchedules_ChangingDateCompletingAndDeletingSynchronizeJobs()
    {
        await using var db = new RoutineEscapeDbContext(Options()); await Seed(db, Intent.Task);
        var task = await db.Tasks.SingleAsync();
        task.ChangeDeadline(Due, false); await db.SaveChangesAsync(); Assert.Empty(db.NotificationSchedules);
        task.ChangeDeadline(Due.AddHours(1), true); await db.SaveChangesAsync();
        var oldToken = (await db.NotificationSchedules.SingleAsync()).ActionToken;
        task.ChangeDeadline(Due.AddHours(2), true); await db.SaveChangesAsync();
        var job = await db.NotificationSchedules.SingleAsync();
        Assert.NotEqual(oldToken, job.ActionToken); Assert.Equal(Due.AddHours(2), job.NextAttemptAt);
        task.Rename("Changed title"); await db.SaveChangesAsync(); Assert.Equal(job.ActionToken, (await db.NotificationSchedules.SingleAsync()).ActionToken);
        task.Complete(Due); await db.SaveChangesAsync(); Assert.Empty(db.NotificationSchedules);
        task.Reopen(); await db.SaveChangesAsync(); Assert.Single(db.NotificationSchedules);
        db.Tasks.Remove(task); await db.SaveChangesAsync(); Assert.Empty(db.NotificationSchedules);
    }

    [Fact]
    public async Task DowntimeCatchesUpOnceWithinDay_AndExpiresOlderDelivery()
    {
        await using var db = new RoutineEscapeDbContext(Options()); await Seed(db, Intent.Reminder);
        var sender = new Sender();
        Assert.True(await Processor(db, sender).ProcessOneAsync(Due.AddHours(2), default));
        Assert.Single(sender.Messages);
        Assert.True(await Processor(db, sender).ProcessOneAsync(Due.AddDays(2), default));
        Assert.Single(sender.Messages); Assert.Equal(4, (await db.NotificationSchedules.SingleAsync()).Stage);
    }

    [Fact]
    public async Task EightFailuresSuspendDelivery_AndCancellationRemovesPendingSchedule()
    {
        await using var db = new RoutineEscapeDbContext(Options()); await Seed(db, Intent.Reminder);
        var sender = new Sender { Fail = true }; var processor = Processor(db, sender);
        var job = await db.NotificationSchedules.SingleAsync();
        for (var attempt = 0; attempt < 8; attempt++)
            Assert.True(await processor.ProcessOneAsync(job.NextAttemptAt!.Value, default));
        Assert.Equal(5, job.Stage); Assert.Null(job.NextAttemptAt);
        Assert.False(await processor.ProcessOneAsync(Due.AddHours(3), default));
        (await db.Reminders.SingleAsync()).Cancel(); await db.SaveChangesAsync(); Assert.Empty(db.NotificationSchedules);
    }

    [Fact]
    public async Task HourSnoozeUsesClickTimeAndInvalidatesPreviousRepeat()
    {
        await using var db = new RoutineEscapeDbContext(Options()); await Seed(db, Intent.Reminder);
        var sender = new Sender(); var processor = Processor(db, sender);
        await processor.ProcessOneAsync(Due, default);
        await processor.ActAsync(123, sender.Messages.Single().ActionToken, "60", Due.AddMinutes(3), default);
        Assert.Equal(Due.AddMinutes(63), (await db.Reminders.SingleAsync()).TriggerAtUtc);
        Assert.False(await processor.ProcessOneAsync(Due.AddMinutes(10), default));
        Assert.True(await processor.ProcessOneAsync(Due.AddMinutes(63), default));
    }

    [PostgreSqlFact]
    public async Task PostgreSql_ConcurrentWorkersSkipLockedRow_AndPersistResult()
    {
        var connectionString = Environment.GetEnvironmentVariable(PostgreSqlFactAttribute.ConnectionStringVariable)!;
        PostgreSqlFactAttribute.RequireIsolatedDatabase(connectionString);
        var options = new DbContextOptionsBuilder<RoutineEscapeDbContext>().UseNpgsql(connectionString).Options;
        Guid owner;
        await using (var setup = new RoutineEscapeDbContext(options))
        {
            await setup.Database.MigrateAsync(); owner = await Seed(setup, Intent.Reminder, Random.Shared.NextInt64(1000, long.MaxValue));
        }
        try
        {
            var blocking = new BlockingSender();
            await using var firstDb = new RoutineEscapeDbContext(options); await using var secondDb = new RoutineEscapeDbContext(options);
            var first = Processor(firstDb, blocking).ProcessOneAsync(Due, default);
            await blocking.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var secondSender = new Sender();
            try { Assert.False(await Processor(secondDb, secondSender).ProcessOneAsync(Due, default)); }
            finally { blocking.Release.TrySetResult(); }
            Assert.True(await first); Assert.Empty(secondSender.Messages);
            await using var verify = new RoutineEscapeDbContext(options);
            Assert.Equal(1, (await verify.NotificationSchedules.SingleAsync(item => item.UserId == owner)).Stage);
        }
        finally
        {
            await using var cleanup = new RoutineEscapeDbContext(options);
            await cleanup.Users.Where(item => item.Id == owner).ExecuteDeleteAsync(); // only this isolated test's user, never a real account
        }
    }

    private static NotificationProcessor Processor(RoutineEscapeDbContext db, INotificationSender sender) => new(db, sender, NullLogger<NotificationProcessor>.Instance);
    private static async Task<Guid> Seed(RoutineEscapeDbContext db, Intent intent, long telegramId = 123)
    {
        var user = new AppUser(Guid.NewGuid(), telegramId, "Test", null, null, Due.AddHours(-1), "Asia/Qyzylorda");
        db.Users.Add(user);
        if (intent == Intent.Task) db.Tasks.Add(new TaskItem(Guid.NewGuid(), user.Id, "Task", Due.AddHours(-1), deadlineUtc: Due, hasExplicitTime: true));
        if (intent == Intent.Event) db.Events.Add(new CalendarEvent(Guid.NewGuid(), user.Id, "Event", Due, Due.AddHours(-1)));
        if (intent == Intent.Reminder) db.Reminders.Add(new Reminder(Guid.NewGuid(), user.Id, "Reminder", Due, Due.AddHours(-1)));
        await db.SaveChangesAsync(); return user.Id;
    }
    private sealed class Sender : INotificationSender
    {
        public bool Fail { get; set; }
        public List<ScheduledNotification> Messages { get; } = [];
        public Task SendAsync(ScheduledNotification notification, CancellationToken ct)
        {
            if (Fail) throw new HttpRequestException("simulated Telegram failure");
            Messages.Add(notification); return Task.CompletedTask;
        }
    }
    private sealed class BlockingSender : INotificationSender
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SendAsync(ScheduledNotification notification, CancellationToken ct) { Entered.TrySetResult(); await Release.Task.WaitAsync(ct); }
    }
}
