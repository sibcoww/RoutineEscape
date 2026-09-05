using RoutineEscape.Domain.Common;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Domain.Entities;

public sealed class Reminder : IEntity
{
    private Reminder()
    {
    }

    public Reminder(Guid id, Guid userId, string title, DateTimeOffset triggerAtUtc,
        DateTimeOffset createdAtUtc, string? description = null, Guid? sourceId = null)
    {
        Id = DomainGuard.Required(id, nameof(id));
        UserId = DomainGuard.Required(userId, nameof(userId));
        Title = DomainGuard.Required(title, nameof(title));
        TriggerAtUtc = DomainGuard.Utc(triggerAtUtc, nameof(triggerAtUtc));
        CreatedAt = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SourceId = sourceId;
        Status = ReminderStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTimeOffset TriggerAtUtc { get; private set; }
    public ReminderStatus Status { get; private set; }
    public Guid? SourceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? TriggeredAt { get; private set; }
    public bool IsTriggered => Status == ReminderStatus.Triggered;

    public void MarkTriggered(DateTimeOffset triggeredAtUtc)
    {
        if (Status != ReminderStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending reminder can be triggered.");
        }

        var timestamp = DomainGuard.Utc(triggeredAtUtc, nameof(triggeredAtUtc));
        if (timestamp < TriggerAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(triggeredAtUtc), "A reminder cannot trigger early.");
        }

        Status = ReminderStatus.Triggered;
        TriggeredAt = timestamp;
    }

    public void Cancel()
    {
        if (Status != ReminderStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending reminder can be cancelled.");
        }

        Status = ReminderStatus.Cancelled;
    }
}
