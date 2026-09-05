using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Bot.Telegram.Sources;

public sealed class MessageSourceDisplayFormatter
{
    public string? Format(MessageSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var value = source.SourceType switch
        {
            MessageSourceType.Direct => null,
            MessageSourceType.ForwardedUser => WithUsername(source.DisplayName!, source.Username),
            MessageSourceType.ForwardedHiddenUser => source.DisplayName,
            MessageSourceType.ForwardedChat => WithAuthor(source.ChatTitle!, source.AuthorSignature),
            MessageSourceType.ForwardedChannel => source.ChatTitle,
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };

        return value is null ? null : $"Источник: {value}";
    }

    private static string WithUsername(string displayName, string? username) =>
        username is null ? displayName : $"{displayName} (@{username})";

    private static string WithAuthor(string chatTitle, string? authorSignature) =>
        authorSignature is null ? chatTitle : $"{chatTitle} · {authorSignature}";
}
