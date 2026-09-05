using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.UnitTests.Entities;

public sealed class TaskItemTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_RejectsBlankTitle()
    {
        Assert.Throws<ArgumentException>(() =>
            new TaskItem(Guid.NewGuid(), Guid.NewGuid(), "  ", CreatedAt));
    }

    [Fact]
    public void Complete_MarksPendingTaskAndRecordsTimestamp()
    {
        var task = new TaskItem(Guid.NewGuid(), Guid.NewGuid(), "Send documents", CreatedAt);
        var completedAt = CreatedAt.AddMinutes(5);

        task.Complete(completedAt);

        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.Equal(completedAt, task.CompletedAt);
        Assert.Throws<InvalidOperationException>(() => task.Complete(completedAt));
    }

    [Fact]
    public void Complete_RejectsTimestampBeforeCreation()
    {
        var task = new TaskItem(Guid.NewGuid(), Guid.NewGuid(), "Send documents", CreatedAt);

        Assert.Throws<ArgumentOutOfRangeException>(() => task.Complete(CreatedAt.AddSeconds(-1)));
    }
}
