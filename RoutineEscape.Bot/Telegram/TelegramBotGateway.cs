using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace RoutineEscape.Bot.Telegram;

internal sealed class TelegramBotGateway(ITelegramBotClient botClient) : ITelegramBotGateway
{
    public async Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken,
        IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null)
    {
        InlineKeyboardMarkup? markup = buttons is null
            ? null
            : new InlineKeyboardMarkup(buttons.Select(row => row.Select(button =>
                InlineKeyboardButton.WithCallbackData(button.Text, button.CallbackData))));
        await botClient.SendMessage(chatId, text, replyMarkup: markup, cancellationToken: cancellationToken);
    }

    public async Task AnswerCallbackQueryAsync(string callbackQueryId, string? text,
        CancellationToken cancellationToken) =>
        await botClient.AnswerCallbackQuery(callbackQueryId, text, cancellationToken: cancellationToken);
}
