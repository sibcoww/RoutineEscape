using Microsoft.EntityFrameworkCore;
using RoutineEscape.Application.Abstractions.Persistence;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Infrastructure.Persistence;

public sealed class RoutineEscapeDbContext(DbContextOptions<RoutineEscapeDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<MessageSource> MessageSources => Set<MessageSource>();
    public DbSet<Draft> Drafts => Set<Draft>();
    public DbSet<NotificationSchedule> NotificationSchedules => Set<NotificationSchedule>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.Entity is TaskItem or CalendarEvent or Reminder
            && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            var (id, userId, intent, date, active) = entry.Entity switch
            {
                TaskItem task => (task.Id, task.UserId, Intent.Task, task.DeadlineUtc, task.Status == TaskItemStatus.Pending && task.HasExplicitTime),
                CalendarEvent calendar => (calendar.Id, calendar.UserId, Intent.Event, (DateTimeOffset?)calendar.StartUtc, true),
                Reminder reminder => (reminder.Id, reminder.UserId, Intent.Reminder, (DateTimeOffset?)reminder.TriggerAtUtc, reminder.Status == ReminderStatus.Pending),
                _ => throw new InvalidOperationException(),
            };
            var schedule = await NotificationSchedules.SingleOrDefaultAsync(item => item.RecordId == id && item.Intent == intent, cancellationToken);
            if (!active || date is null || entry.State == EntityState.Deleted)
            {
                if (schedule is not null) NotificationSchedules.Remove(schedule);
                continue;
            }
            if (schedule is not null && schedule.SourceAt == date) continue; // Text edits do not restart delivery.
            if (date <= DateTimeOffset.UtcNow)
            {
                if (schedule is not null) NotificationSchedules.Remove(schedule);
                continue;
            }
            if (schedule is null)
            {
                schedule = new NotificationSchedule { UserId = userId, RecordId = id, Intent = intent };
                NotificationSchedules.Add(schedule);
            }
            schedule.Reset(date.Value);
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RoutineEscapeDbContext).Assembly);
}
