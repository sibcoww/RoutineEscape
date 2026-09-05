using RoutineEscape.Bot.Telegram.Sources;
using RoutineEscape.Domain.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RoutineEscape.IntegrationTests.Telegram;

public sealed class TelegramMessageSourceExtractorTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTime OriginalDate = new(2026, 9, 4, 9, 0, 0, DateTimeKind.Utc);
    private readonly TelegramMessageSourceExtractor _extractor = new();
    private readonly MessageSourceDisplayFormatter _formatter = new();

    [Fact]
    public void Extract_MapsDirectMessage()
    {
        var message = CreateMessage();

        var source = _extractor.Extract(message, ReceivedAt);

        Assert.Equal(MessageSourceType.Direct, source.SourceType);
        Assert.Equal(10, source.TelegramUserId);
        Assert.Null(_formatter.Format(source));
    }

    [Fact]
    public void Extract_MapsKnownUserWithOptionalUsername()
    {
        var message = CreateMessage();
        message.ForwardOrigin = new MessageOriginUser
        {
            Date = OriginalDate,
            SenderUser = new User
            {
                Id = 20,
                FirstName = "Ivan",
                LastName = "Ivanov",
                Username = "ivan",
            },
        };

        var source = _extractor.Extract(message, ReceivedAt);

        Assert.Equal(MessageSourceType.ForwardedUser, source.SourceType);
        Assert.Equal("Ivan Ivanov", source.DisplayName);
        Assert.Equal("Источник: Ivan Ivanov (@ivan)", _formatter.Format(source));
    }

    [Fact]
    public void Extract_MapsHiddenUserWithoutInventingIdentity()
    {
        var message = CreateMessage();
        message.ForwardOrigin = new MessageOriginHiddenUser
        {
            Date = OriginalDate,
            SenderUserName = "Hidden Sender",
        };

        var source = _extractor.Extract(message, ReceivedAt);

        Assert.Equal(MessageSourceType.ForwardedHiddenUser, source.SourceType);
        Assert.True(source.IsHiddenUser);
        Assert.Null(source.TelegramUserId);
        Assert.Null(source.Username);
        Assert.Equal("Источник: Hidden Sender", _formatter.Format(source));
    }

    [Fact]
    public void Extract_MapsChatWithAuthorSignature()
    {
        var message = CreateMessage();
        message.ForwardOrigin = new MessageOriginChat
        {
            Date = OriginalDate,
            SenderChat = new Chat { Id = -100, Type = ChatType.Supergroup, Title = "Work group" },
            AuthorSignature = "Ivan",
        };

        var source = _extractor.Extract(message, ReceivedAt);

        Assert.Equal(MessageSourceType.ForwardedChat, source.SourceType);
        Assert.Equal(-100, source.TelegramChatId);
        Assert.Equal("Источник: Work group · Ivan", _formatter.Format(source));
    }

    [Fact]
    public void Extract_MapsChannelAndOriginalMessageId()
    {
        var message = CreateMessage();
        message.ForwardOrigin = new MessageOriginChannel
        {
            Date = OriginalDate,
            Chat = new Chat { Id = -200, Type = ChatType.Channel, Title = "News" },
            MessageId = 42,
        };

        var source = _extractor.Extract(message, ReceivedAt);

        Assert.Equal(MessageSourceType.ForwardedChannel, source.SourceType);
        Assert.Equal(42, source.TelegramMessageId);
        Assert.Null(source.Username);
        Assert.Null(source.AuthorSignature);
        Assert.Equal("Источник: News", _formatter.Format(source));
    }

    private static Message CreateMessage() => new()
    {
        Id = 1,
        Date = ReceivedAt.UtcDateTime,
        Chat = new Chat { Id = 10, Type = ChatType.Private },
        From = new User { Id = 10, FirstName = "Owner" },
        Text = "Message",
    };
}
