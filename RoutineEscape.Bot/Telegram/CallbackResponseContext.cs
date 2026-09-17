using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram;

// One instance per update scope; never shared with background notifications.
public sealed class CallbackResponseContext
{
    private (long ChatId, int MessageId)? previous;

    public void Begin(CallbackQuery callback)
    {
        previous = callback.Message is { From.IsBot: true } message && message.Chat.Id == callback.From.Id
            ? (message.Chat.Id, message.Id) : null;
    }

    public int? TakePrevious(long chatId)
    {
        if (previous is not { } target || target.ChatId != chatId) return null;
        previous = null;
        return target.MessageId;
    }
}
