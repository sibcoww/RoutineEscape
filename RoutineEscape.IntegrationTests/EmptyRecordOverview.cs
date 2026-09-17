using RoutineEscape.Application.Records;

namespace RoutineEscape.IntegrationTests;

internal sealed class EmptyRecordOverview : IRecordOverviewService
{
    private static readonly RecordOverview Empty = new("UTC", [], 0, 0, 1, 0);
    public Task<RecordOverview> SummaryAsync(long telegramUserId, DateTimeOffset nowUtc, Guid? savedId, CancellationToken cancellationToken) => Task.FromResult(Empty);
    public Task<RecordOverview> PageAsync(long telegramUserId, int page, DateTimeOffset nowUtc, CancellationToken cancellationToken) => Task.FromResult(Empty);
}
