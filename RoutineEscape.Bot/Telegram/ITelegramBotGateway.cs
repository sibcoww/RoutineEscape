namespace RoutineEscape.Bot.Telegram;

public interface ITelegramBotGateway
{
    Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken,
        IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null);
    Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, CancellationToken cancellationToken);
}
