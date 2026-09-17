namespace RoutineEscape.Bot.Telegram;

public interface ITelegramBotGateway
{
    Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken,
        IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null);
    Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, CancellationToken cancellationToken);
    Task SendHtmlMessageAsync(long chatId, string html, CancellationToken cancellationToken,
        IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null) => SendTextMessageAsync(chatId, html, cancellationToken, buttons);
}
