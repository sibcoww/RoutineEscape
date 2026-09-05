using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.UnitTests.Entities;

public sealed class MessageSourceTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ForwardedHiddenUser_DoesNotInventTelegramIdentity()
    {
        var source = MessageSource.ForwardedHiddenUser(
            Guid.NewGuid(), "Ivan Ivanov", Timestamp.AddDays(-1), Timestamp);

        Assert.Equal(MessageSourceType.ForwardedHiddenUser, source.SourceType);
        Assert.True(source.IsHiddenUser);
        Assert.Null(source.TelegramUserId);
        Assert.Null(source.Username);
        Assert.Equal("Ivan Ivanov", source.DisplayName);
    }

    [Fact]
    public void ForwardedChannel_PreservesNegativeTelegramChatId()
    {
        var source = MessageSource.ForwardedChannel(
            Guid.NewGuid(), -1001234567890, 17, "News", Timestamp.AddDays(-1), Timestamp);

        Assert.Equal(MessageSourceType.ForwardedChannel, source.SourceType);
        Assert.Equal(-1001234567890, source.TelegramChatId);
        Assert.Equal(17, source.TelegramMessageId);
    }
}
