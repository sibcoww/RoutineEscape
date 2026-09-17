using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Records;

public sealed record SavedRecord(Guid Id, Intent Intent, string Title, DateTimeOffset? AtUtc, DateTimeOffset CreatedAt, bool HasExplicitTime = false, string Status = "Active", DateTimeOffset? EndUtc = null);
public sealed record RecordOverview(string TimeZoneId, IReadOnlyList<SavedRecord> Items, int TotalCount, int Page, int PageCount, int RemainingCount);

public interface IRecordOverviewService
{
    Task<RecordOverview> SummaryAsync(long telegramUserId, DateTimeOffset nowUtc, Guid? savedId, CancellationToken cancellationToken);
    Task<RecordOverview> PageAsync(long telegramUserId, int page, DateTimeOffset nowUtc, CancellationToken cancellationToken);
}
