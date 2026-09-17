using RoutineEscape.Application.Abstractions.Persistence;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Records;

public sealed class RecordOverviewService(IRepository<AppUser> users, IRepository<TaskItem> tasks,
    IRepository<CalendarEvent> events, IRepository<Reminder> reminders, IRepository<Note> notes) : IRecordOverviewService, IRecordBrowserService
{
    public const int PageSize = 5;

    public async Task<RecordOverview> SummaryAsync(long telegramUserId, DateTimeOffset nowUtc, Guid? savedId, CancellationToken cancellationToken)
    {
        var (zone, records) = await LoadAsync(telegramUserId, nowUtc, cancellationToken);
        var today = TimeZoneInfo.ConvertTime(nowUtc, zone).Date;
        var others = records.Where(item => item.Id != savedId).ToArray();
        var dated = others.Where(item => item.AtUtc is { } at && TimeZoneInfo.ConvertTime(at, zone).Date >= today
            && TimeZoneInfo.ConvertTime(at, zone).Date < today.AddDays(3)).OrderBy(item => item.AtUtc).ThenBy(item => item.Id).ToArray();
        var undated = others.Where(item => item.AtUtc is null).OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).ToArray();
        // Reserve up to two slots for fresh notes/undated tasks, then use any free slots for dated items.
        var datedCount = Math.Min(dated.Length, PageSize - Math.Min(2, undated.Length));
        var pickedUndated = undated.Take(PageSize - datedCount).ToArray();
        var selected = dated.Take(PageSize - pickedUndated.Length).Concat(pickedUndated).ToArray();
        return new RecordOverview(zone.Id, selected, records.Count, 0, Pages(records.Count), others.Length - selected.Length);
    }

    public async Task<RecordOverview> PageAsync(long telegramUserId, int page, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        if (page < 0) throw new FormatException("Invalid page.");
        var (zone, records) = await LoadAsync(telegramUserId, nowUtc, cancellationToken);
        var pages = Pages(records.Count);
        page = Math.Min(page, pages - 1);
        var items = records.OrderBy(item => item.AtUtc is null ? 1 : 0).ThenBy(item => item.AtUtc)
            .ThenByDescending(item => item.CreatedAt).ThenBy(item => item.Intent).ThenBy(item => item.Id)
            .Skip(page * PageSize).Take(PageSize).ToArray();
        return new RecordOverview(zone.Id, items, records.Count, page, pages, records.Count - items.Length);
    }

    public async Task<RecordOverview> BrowseAsync(long userId, RecordView view, string? query, int page, DateTimeOffset now, CancellationToken ct)
    {
        if (!Enum.IsDefined(view) || page < 0) throw new FormatException("Invalid view or page.");
        query = query?.Trim();
        if (view == RecordView.Search && (string.IsNullOrWhiteSpace(query) || query.Length > 100))
            throw new FormatException("Search must contain 1–100 characters.");
        var (zone, records) = await LoadAsync(userId, now, ct, view is RecordView.Search or RecordView.Completed, view == RecordView.Search ? query : null);
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        bool InWindow(SavedRecord item, int days) => item.AtUtc is { } at &&
            TimeZoneInfo.ConvertTime(at, zone).Date < today.AddDays(days) &&
            TimeZoneInfo.ConvertTime(item.EndUtc ?? at, zone).Date >= today;
        var filtered = records.Where(item => view switch
        {
            RecordView.Today => InWindow(item, 1),
            RecordView.Upcoming => InWindow(item, 7),
            RecordView.Overdue => RecordTiming.IsOverdue(item, zone, now),
            RecordView.Completed => item.Status is "Completed" or "Triggered",
            RecordView.Tasks => item.Intent == Intent.Task,
            RecordView.Events => item.Intent == Intent.Event,
            RecordView.Reminders => item.Intent == Intent.Reminder,
            RecordView.Notes => item.Intent == Intent.Note,
            _ => true,
        }).ToArray();
        var pages = Pages(filtered.Length);
        page = Math.Min(page, pages - 1);
        var ordered = view is RecordView.Search or RecordView.Completed or RecordView.Notes
            ? filtered.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            : filtered.OrderBy(item => item.AtUtc is null).ThenBy(item => item.AtUtc).ThenByDescending(item => item.CreatedAt).ThenBy(item => item.Id);
        var items = ordered.Skip(page * PageSize).Take(PageSize).ToArray();
        return new(zone.Id, items, filtered.Length, page, pages, filtered.Length - items.Length);
    }

    private async Task<(TimeZoneInfo Zone, List<SavedRecord> Records)> LoadAsync(long telegramUserId, DateTimeOffset nowUtc, CancellationToken ct, bool includeHistory = false, string? query = null)
    {
        var user = (await users.ListAsync(user => user.TelegramUserId == telegramUserId, ct)).SingleOrDefault();
        if (user is null) return (TimeZoneInfo.Utc, []);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZoneId);
        var today = TimeZoneInfo.ConvertTime(nowUtc, zone).Date;
        var result = new List<SavedRecord>();
        bool Matches(params string?[] texts) => query is null || texts.Any(text => text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
        result.AddRange((await tasks.ListAsync(item => item.UserId == user.Id && (includeHistory || item.Status == TaskItemStatus.Pending), ct))
            .Where(item => Matches(item.Title, item.Description, item.OriginalText))
            .Select(item => new SavedRecord(item.Id, Intent.Task, item.Title, item.DeadlineUtc, item.CreatedAt, item.HasExplicitTime, item.Status.ToString())));
        result.AddRange((await events.ListAsync(item => item.UserId == user.Id, ct))
            .Where(item => (includeHistory || TimeZoneInfo.ConvertTime(item.EndUtc ?? item.StartUtc, zone).Date >= today) && Matches(item.Title, item.Description))
            .Select(item => new SavedRecord(item.Id, Intent.Event, item.Title, item.StartUtc, item.CreatedAt, true, (item.EndUtc ?? item.StartUtc) < nowUtc ? "Past" : "Active", item.EndUtc)));
        result.AddRange((await reminders.ListAsync(item => item.UserId == user.Id && (includeHistory || item.Status == ReminderStatus.Pending), ct))
            .Where(item => Matches(item.Title, item.Description))
            .Select(item => new SavedRecord(item.Id, Intent.Reminder, item.Title, item.TriggerAtUtc, item.CreatedAt, true, item.Status.ToString())));
        result.AddRange((await notes.ListAsync(item => item.UserId == user.Id, ct))
            .Where(item => Matches(item.Title, item.Content))
            .Select(item => new SavedRecord(item.Id, Intent.Note, item.Title ?? item.Content, null, item.CreatedAt)));
        return (zone, result);
    }

    private static int Pages(int count) => Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
}
