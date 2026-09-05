using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.UnitTests.Entities;

public sealed class ReminderTests
{
    [Fact]
    public void MarkTriggered_RejectsEarlyTrigger()
    {
        var createdAt = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);
        var reminder = new Reminder(Guid.NewGuid(), Guid.NewGuid(), "Turn off oven",
            createdAt.AddMinutes(40), createdAt);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            reminder.MarkTriggered(createdAt.AddMinutes(39)));
        Assert.Equal(ReminderStatus.Pending, reminder.Status);
    }

    [Fact]
    public void MarkTriggered_RecordsSuccessfulTrigger()
    {
        var createdAt = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);
        var triggerAt = createdAt.AddMinutes(40);
        var reminder = new Reminder(Guid.NewGuid(), Guid.NewGuid(), "Turn off oven", triggerAt, createdAt);

        reminder.MarkTriggered(triggerAt);

        Assert.True(reminder.IsTriggered);
        Assert.Equal(triggerAt, reminder.TriggeredAt);
    }
}
