using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RoutineEscape.Application.Records;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Infrastructure.Persistence;

public sealed class NotificationProcessor(RoutineEscapeDbContext db, INotificationSender sender, ILogger<NotificationProcessor> logger)
{
    private static readonly SemaphoreSlim TestGate = new(1, 1);

    public async Task<bool> ProcessOneAsync(DateTimeOffset now, CancellationToken ct)
    {
        var relational = db.Database.IsRelational();
        if (!relational) await TestGate.WaitAsync(ct);
        await using var transaction = relational ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var schedule = relational
                ? await db.NotificationSchedules.FromSqlInterpolated($"SELECT * FROM notification_schedules WHERE next_attempt_at <= {now} AND stage < 2 ORDER BY next_attempt_at, id LIMIT 1 FOR UPDATE SKIP LOCKED").FirstOrDefaultAsync(ct)
                : await db.NotificationSchedules.Where(item => item.NextAttemptAt <= now && item.Stage < 2).OrderBy(item => item.NextAttemptAt).FirstOrDefaultAsync(ct);
            if (schedule is null) return false;
            var source = await SourceAsync(schedule, ct);
            var user = await db.Users.SingleOrDefaultAsync(item => item.Id == schedule.UserId, ct);
            if (source is null || user is null)
                db.NotificationSchedules.Remove(schedule);
            else if (now - schedule.DueAt > TimeSpan.FromHours(24))
            {
                schedule.Stage = 4; schedule.NextAttemptAt = null;
                logger.LogInformation("Notification expired after downtime: schedule={ScheduleId}", schedule.Id);
            }
            else
            {
                using var scope = logger.BeginScope("NotificationId={NotificationId} Stage={Stage}", schedule.Id, schedule.Stage);
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    await sender.SendAsync(new ScheduledNotification(user.TelegramUserId, schedule.Intent, Title(source),
                        user.TimeZoneId, schedule.SourceAt, schedule.ActionToken, schedule.Stage == 1), timeout.Token);
                    schedule.LastSentAt = now;
                    schedule.Attempts = 0;
                    schedule.Stage++;
                    schedule.DueAt = now.AddMinutes(10);
                    schedule.NextAttemptAt = schedule.Stage == 1 ? schedule.DueAt : null;
                    logger.LogInformation("Scheduled notification accepted; nextStage={Stage}", schedule.Stage);
                }
                catch (Exception error) when (!ct.IsCancellationRequested)
                {
                    schedule.Attempts++;
                    if (schedule.Attempts >= 8) { schedule.Stage = 5; schedule.NextAttemptAt = null; }
                    else schedule.NextAttemptAt = now.AddSeconds(Math.Min(900, 30 * Math.Pow(2, schedule.Attempts - 1)));
                    logger.LogError(error, "Scheduled notification failed: attempt={Attempt} suspended={Suspended}", schedule.Attempts, schedule.Stage == 5);
                }
            }
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            return true;
        }
        finally { if (!relational) TestGate.Release(); }
    }

    public async Task<string?> ActAsync(long telegramUserId, Guid token, string action, DateTimeOffset now, CancellationToken ct)
    {
        if (action is not ("done" or "10" or "60")) return null;
        var relational = db.Database.IsRelational();
        if (!relational) await TestGate.WaitAsync(ct);
        await using var transaction = relational ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var schedule = relational
                ? await db.NotificationSchedules.FromSqlInterpolated($"SELECT * FROM notification_schedules WHERE action_token = {token} FOR UPDATE").FirstOrDefaultAsync(ct)
                : await db.NotificationSchedules.SingleOrDefaultAsync(item => item.ActionToken == token, ct);
            if (schedule is null) return null;
            var user = await db.Users.SingleAsync(item => item.Id == schedule.UserId, ct);
            if (user.TelegramUserId != telegramUserId) return null;
            var source = await SourceAsync(schedule, ct);
            if (source is null) return null;
            string result;
            if (action == "done")
            {
                switch (source)
                {
                    case TaskItem task: task.Complete(now); break;
                    case Reminder reminder: reminder.MarkTriggered(now); break;
                    case CalendarEvent: schedule.Stage = 3; schedule.NextAttemptAt = null; schedule.ActionToken = Guid.NewGuid(); break;
                }
                result = schedule.Intent == Intent.Event ? "Уведомление о событии подтверждено. Повтора не будет." : "Готово. Запись завершена, повтора не будет.";
            }
            else
            {
                var at = now.AddMinutes(action == "10" ? 10 : 60);
                switch (source)
                {
                    case TaskItem task: task.ChangeDeadline(at, true); break;
                    case Reminder reminder: reminder.Reschedule(at); break;
                    case CalendarEvent: break; // Snooze the notification, not the calendar event itself.
                }
                var sourceAt = schedule.SourceAt;
                schedule.Reset(at);
                if (schedule.Intent == Intent.Event) schedule.SourceAt = sourceAt;
                result = action == "10" ? "Отложено на 10 минут. Начат новый цикл уведомлений." : "Отложено на час. Начат новый цикл уведомлений.";
            }
            await db.SaveChangesAsync(ct);
            if (transaction is not null) await transaction.CommitAsync(ct);
            logger.LogInformation("Notification action accepted: action={Action} record={RecordId}", action, schedule.RecordId);
            return result;
        }
        finally { if (!relational) TestGate.Release(); }
    }

    private async Task<object?> SourceAsync(NotificationSchedule schedule, CancellationToken ct) => schedule.Intent switch
    {
        Intent.Task => await db.Tasks.SingleOrDefaultAsync(item => item.Id == schedule.RecordId && item.UserId == schedule.UserId && item.Status == TaskItemStatus.Pending && item.HasExplicitTime, ct),
        Intent.Event => await db.Events.SingleOrDefaultAsync(item => item.Id == schedule.RecordId && item.UserId == schedule.UserId, ct),
        Intent.Reminder => await db.Reminders.SingleOrDefaultAsync(item => item.Id == schedule.RecordId && item.UserId == schedule.UserId && item.Status == ReminderStatus.Pending, ct),
        _ => null,
    };
    private static string Title(object source) => source switch { TaskItem task => task.Title, CalendarEvent calendar => calendar.Title, Reminder reminder => reminder.Title, _ => "" };
}
