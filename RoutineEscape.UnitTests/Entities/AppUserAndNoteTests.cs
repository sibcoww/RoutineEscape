using RoutineEscape.Domain.Entities;

namespace RoutineEscape.UnitTests.Entities;

public sealed class AppUserAndNoteTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AppUser_UsesMvpDefaultsAndNormalizesUsername()
    {
        var user = new AppUser(Guid.NewGuid(), 123, "Ivan", null, "@ivan", CreatedAt);

        Assert.Equal("Asia/Almaty", user.TimeZoneId);
        Assert.Equal("ru", user.Language);
        Assert.Equal("ivan", user.Username);
    }

    [Fact]
    public void Note_NormalizesAndDeduplicatesTags()
    {
        var note = new Note(Guid.NewGuid(), Guid.NewGuid(), "Room 305", CreatedAt,
            tags: ["#University", "university", " defense "]);

        Assert.Equal(2, note.Tags.Count);
        Assert.Contains("University", note.Tags);
        Assert.Contains("defense", note.Tags);
    }
}
