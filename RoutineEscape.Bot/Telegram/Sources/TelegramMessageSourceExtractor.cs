using RoutineEscape.Domain.Entities;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Sources;

public sealed class TelegramMessageSourceExtractor : IMessageSourceExtractor
{
    public MessageSource Extract(Message message, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.ForwardOrigin switch
        {
            MessageOriginUser origin => ExtractUser(origin, createdAtUtc),
            MessageOriginHiddenUser origin => MessageSource.ForwardedHiddenUser(
                Guid.NewGuid(), origin.SenderUserName, AsUtc(origin.Date), createdAtUtc),
            MessageOriginChat origin => MessageSource.ForwardedChat(
                Guid.NewGuid(), origin.SenderChat.Id, RequiredTitle(origin.SenderChat),
                AsUtc(origin.Date), createdAtUtc, origin.SenderChat.Username, origin.AuthorSignature),
            MessageOriginChannel origin => MessageSource.ForwardedChannel(
                Guid.NewGuid(), origin.Chat.Id, origin.MessageId, RequiredTitle(origin.Chat),
                AsUtc(origin.Date), createdAtUtc, origin.Chat.Username, origin.AuthorSignature),
            null => ExtractDirect(message, createdAtUtc),
            _ => throw new NotSupportedException(
                $"Forward origin type '{message.ForwardOrigin.GetType().Name}' is not supported."),
        };
    }

    private static MessageSource ExtractUser(MessageOriginUser origin, DateTimeOffset createdAtUtc)
    {
        var user = origin.SenderUser;
        var displayName = string.Join(' ', new[] { user.FirstName, user.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        return MessageSource.ForwardedUser(Guid.NewGuid(), user.Id, displayName,
            AsUtc(origin.Date), createdAtUtc, user.Username);
    }

    private static MessageSource ExtractDirect(Message message, DateTimeOffset createdAtUtc)
    {
        var sender = message.From ?? throw new InvalidOperationException(
            "A direct message must contain its sender.");
        return MessageSource.Direct(Guid.NewGuid(), sender.Id, createdAtUtc);
    }

    private static string RequiredTitle(Chat chat) =>
        string.IsNullOrWhiteSpace(chat.Title)
            ? throw new InvalidOperationException("A forwarded chat or channel must have a title.")
            : chat.Title;

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(), TimeSpan.Zero);
}
