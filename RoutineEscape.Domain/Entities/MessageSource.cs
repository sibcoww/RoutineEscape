using RoutineEscape.Domain.Common;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Domain.Entities;

public sealed class MessageSource : IEntity
{
    private MessageSource()
    {
    }

    private MessageSource(Guid id, MessageSourceType sourceType, DateTimeOffset createdAtUtc)
    {
        Id = DomainGuard.Required(id, nameof(id));
        SourceType = sourceType;
        CreatedAt = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }
    public MessageSourceType SourceType { get; private set; }
    public long? TelegramUserId { get; private set; }
    public long? TelegramChatId { get; private set; }
    public long? TelegramMessageId { get; private set; }
    public string? Username { get; private set; }
    public string? DisplayName { get; private set; }
    public string? ChatTitle { get; private set; }
    public string? AuthorSignature { get; private set; }
    public DateTimeOffset? OriginalMessageDateUtc { get; private set; }
    public bool IsHiddenUser { get; private set; }
    public string? RawMetadataJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static MessageSource Direct(Guid id, long telegramUserId, DateTimeOffset createdAtUtc) =>
        new(id, MessageSourceType.Direct, createdAtUtc)
        {
            TelegramUserId = Positive(telegramUserId, nameof(telegramUserId)),
        };

    public static MessageSource ForwardedUser(Guid id, long telegramUserId, string displayName,
        DateTimeOffset originalMessageDateUtc, DateTimeOffset createdAtUtc, string? username = null,
        string? rawMetadataJson = null) =>
        new(id, MessageSourceType.ForwardedUser, createdAtUtc)
        {
            TelegramUserId = Positive(telegramUserId, nameof(telegramUserId)),
            DisplayName = DomainGuard.Required(displayName, nameof(displayName)),
            Username = Optional(username)?.TrimStart('@'),
            OriginalMessageDateUtc = DomainGuard.Utc(originalMessageDateUtc, nameof(originalMessageDateUtc)),
            RawMetadataJson = Optional(rawMetadataJson),
        };

    public static MessageSource ForwardedHiddenUser(Guid id, string displayName,
        DateTimeOffset originalMessageDateUtc, DateTimeOffset createdAtUtc, string? rawMetadataJson = null) =>
        new(id, MessageSourceType.ForwardedHiddenUser, createdAtUtc)
        {
            DisplayName = DomainGuard.Required(displayName, nameof(displayName)),
            OriginalMessageDateUtc = DomainGuard.Utc(originalMessageDateUtc, nameof(originalMessageDateUtc)),
            IsHiddenUser = true,
            RawMetadataJson = Optional(rawMetadataJson),
        };

    public static MessageSource ForwardedChat(Guid id, long telegramChatId, string chatTitle,
        DateTimeOffset originalMessageDateUtc, DateTimeOffset createdAtUtc, string? username = null,
        string? authorSignature = null, string? rawMetadataJson = null) =>
        ForwardedConversation(id, MessageSourceType.ForwardedChat, telegramChatId, null, chatTitle,
            originalMessageDateUtc, createdAtUtc, username, authorSignature, rawMetadataJson);

    public static MessageSource ForwardedChannel(Guid id, long telegramChatId, long telegramMessageId,
        string chatTitle, DateTimeOffset originalMessageDateUtc, DateTimeOffset createdAtUtc,
        string? username = null, string? authorSignature = null, string? rawMetadataJson = null) =>
        ForwardedConversation(id, MessageSourceType.ForwardedChannel, telegramChatId,
            Positive(telegramMessageId, nameof(telegramMessageId)), chatTitle, originalMessageDateUtc,
            createdAtUtc, username, authorSignature, rawMetadataJson);

    private static MessageSource ForwardedConversation(Guid id, MessageSourceType type,
        long telegramChatId, long? telegramMessageId, string chatTitle,
        DateTimeOffset originalMessageDateUtc, DateTimeOffset createdAtUtc, string? username,
        string? authorSignature, string? rawMetadataJson) =>
        new(id, type, createdAtUtc)
        {
            TelegramChatId = NonZero(telegramChatId, nameof(telegramChatId)),
            TelegramMessageId = telegramMessageId,
            ChatTitle = DomainGuard.Required(chatTitle, nameof(chatTitle)),
            Username = Optional(username)?.TrimStart('@'),
            AuthorSignature = Optional(authorSignature),
            OriginalMessageDateUtc = DomainGuard.Utc(originalMessageDateUtc, nameof(originalMessageDateUtc)),
            RawMetadataJson = Optional(rawMetadataJson),
        };

    private static long Positive(long value, string parameterName) => value > 0
        ? value
        : throw new ArgumentOutOfRangeException(parameterName);

    private static long NonZero(long value, string parameterName) => value != 0
        ? value
        : throw new ArgumentOutOfRangeException(parameterName);

    private static string? Optional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
