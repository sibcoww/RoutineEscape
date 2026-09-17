using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Records;

public sealed record ScheduledNotification(long TelegramUserId, Intent Intent, string Title, string TimeZoneId,
    DateTimeOffset AtUtc, Guid ActionToken, bool IsFollowUp);
public interface INotificationSender
{
    Task SendAsync(ScheduledNotification notification, CancellationToken ct);
}
