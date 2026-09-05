using RoutineEscape.Domain.Entities;

namespace RoutineEscape.UnitTests.Entities;

public sealed class CalendarEventTests
{
    [Fact]
    public void Constructor_RejectsEndAtOrBeforeStart()
    {
        var start = new DateTimeOffset(2026, 9, 6, 15, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CalendarEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Interview", start, start.AddDays(-1), endUtc: start));
    }

    [Fact]
    public void Constructor_RejectsNonUtcTimestamp()
    {
        var local = new DateTimeOffset(2026, 9, 6, 15, 0, 0, TimeSpan.FromHours(5));

        Assert.Throws<ArgumentException>(() => new CalendarEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Interview", local, local));
    }
}
