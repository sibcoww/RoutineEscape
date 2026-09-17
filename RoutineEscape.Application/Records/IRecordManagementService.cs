using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Records;

public sealed record RecordDetails(Guid Id, Intent Intent, string Text, DateTimeOffset? AtUtc, string TimeZoneId, string Status, bool HasExplicitTime = false);
public sealed class RecordInputException(string message) : Exception(message);

public interface IRecordManagementService
{
    Task<RecordDetails> GetAsync(long userId, Intent intent, Guid id, CancellationToken ct);
    Task<RecordDetails> SetCompletedAsync(long userId, Guid id, bool completed, DateTimeOffset now, CancellationToken ct);
    Task<RecordDetails> EditAsync(long userId, Intent intent, Guid id, string field, string input, DateTimeOffset now, CancellationToken ct);
    Task DeleteAsync(long userId, Intent intent, Guid id, CancellationToken ct);
}
