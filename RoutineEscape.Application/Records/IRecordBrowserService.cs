using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Records;

public enum RecordView { All, Today, Upcoming, Overdue, Completed, Tasks, Events, Reminders, Notes, Search }

public interface IRecordBrowserService
{
    Task<RecordOverview> BrowseAsync(long userId, RecordView view, string? query, int page, DateTimeOffset now, CancellationToken ct);
}

public static class RecordTiming
{
    public static bool IsOverdue(SavedRecord item, TimeZoneInfo zone, DateTimeOffset now) =>
        item.Status is "Active" or "Pending" && item.AtUtc is { } at &&
        (item.Intent == Intent.Reminder ? at < now : item.Intent == Intent.Task &&
            (item.HasExplicitTime ? at < now : TimeZoneInfo.ConvertTime(at, zone).Date < TimeZoneInfo.ConvertTime(now, zone).Date));
}
