using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.UnitTests.Entities;

public sealed class DraftTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void Constructor_RejectsConfidenceOutsideUnitInterval(decimal confidence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(confidence));
    }

    [Fact]
    public void Constructor_RejectsMalformedPayload()
    {
        Assert.Throws<ArgumentException>(() => new Draft(Guid.NewGuid(), Guid.NewGuid(), 42,
            Intent.Task, 0.9m, "not-json", CreatedAt.AddMinutes(30), CreatedAt));
    }

    [Fact]
    public void Confirm_ChangesPendingDraftState()
    {
        var draft = Create(0.9m);

        draft.Confirm(CreatedAt.AddMinutes(1));

        Assert.Equal(DraftStatus.Confirmed, draft.Status);
        Assert.Throws<InvalidOperationException>(draft.Cancel);
    }

    [Fact]
    public void Confirm_ExpiresDraftWhenDeadlinePassed()
    {
        var draft = Create(0.9m);

        Assert.Throws<InvalidOperationException>(() => draft.Confirm(CreatedAt.AddHours(1)));
        Assert.Equal(DraftStatus.Expired, draft.Status);
    }

    private static Draft Create(decimal confidence) => new(Guid.NewGuid(), Guid.NewGuid(), 42,
        Intent.Task, confidence, "{\"title\":\"Send documents\"}", CreatedAt.AddMinutes(30), CreatedAt);
}
