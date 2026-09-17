using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Domain.Entities;

public sealed class NotificationSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid RecordId { get; set; }
    public Intent Intent { get; set; }
    public DateTimeOffset SourceAt { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public Guid ActionToken { get; set; } = Guid.NewGuid();
    // 0 first delivery; 1 single follow-up; 2 waiting for acknowledgement; 3 acknowledged; 4 expired; 5 failed.
    public int Stage { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? LastSentAt { get; set; }

    public void Reset(DateTimeOffset at)
    {
        SourceAt = DueAt = at;
        NextAttemptAt = at;
        Stage = Attempts = 0;
        LastSentAt = null;
        ActionToken = Guid.NewGuid();
    }
}
