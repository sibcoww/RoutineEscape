using RoutineEscape.Domain.Common;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Domain.Entities;

public sealed class TaskItem : IEntity
{
    private TaskItem()
    {
    }

    public TaskItem(Guid id, Guid userId, string title, DateTimeOffset createdAtUtc,
        string? description = null, DateTimeOffset? deadlineUtc = null,
        TaskPriority priority = TaskPriority.Normal, Guid? sourceId = null, string? originalText = null)
    {
        Id = DomainGuard.Required(id, nameof(id));
        UserId = DomainGuard.Required(userId, nameof(userId));
        Title = DomainGuard.Required(title, nameof(title));
        CreatedAt = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        Description = NormalizeOptional(description);
        DeadlineUtc = deadlineUtc is null ? null : DomainGuard.Utc(deadlineUtc.Value, nameof(deadlineUtc));
        Priority = priority;
        SourceId = sourceId;
        OriginalText = NormalizeOptional(originalText);
        Status = TaskItemStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTimeOffset? DeadlineUtc { get; private set; }
    public TaskPriority Priority { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public Guid? SourceId { get; private set; }
    public string? OriginalText { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        if (Status != TaskItemStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending task can be completed.");
        }

        var timestamp = DomainGuard.Utc(completedAtUtc, nameof(completedAtUtc));
        if (timestamp < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(completedAtUtc));
        }

        Status = TaskItemStatus.Completed;
        CompletedAt = timestamp;
    }

    public void Cancel()
    {
        if (Status != TaskItemStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending task can be cancelled.");
        }

        Status = TaskItemStatus.Cancelled;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
