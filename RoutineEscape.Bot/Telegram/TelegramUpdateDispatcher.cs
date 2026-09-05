using RoutineEscape.Bot.Telegram.Handlers;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram;

public sealed class TelegramUpdateDispatcher(
    StartCommandHandler startCommandHandler,
    TextMessageHandler textMessageHandler,
    CallbackQueryHandler callbackQueryHandler,
    ILogger<TelegramUpdateDispatcher> logger) : ITelegramUpdateDispatcher
{
    public Task DispatchAsync(Update update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        logger.LogInformation("Processing Telegram update {UpdateId} of type {UpdateType}", update.Id, update.Type);

        if (update.CallbackQuery is not null)
        {
            return callbackQueryHandler.HandleAsync(update.CallbackQuery, cancellationToken);
        }

        if (update.Message?.Text is not { } text)
        {
            logger.LogDebug("Telegram update {UpdateId} has no supported content", update.Id);
            return Task.CompletedTask;
        }

        return IsStartCommand(text)
            ? startCommandHandler.HandleAsync(update.Message, cancellationToken)
            : textMessageHandler.HandleAsync(update.Message, cancellationToken);
    }

    private static bool IsStartCommand(string text)
    {
        var command = text.Split(' ', 2, StringSplitOptions.TrimEntries)[0];
        return command.Equals("/start", StringComparison.OrdinalIgnoreCase)
            || command.StartsWith("/start@", StringComparison.OrdinalIgnoreCase);
    }
}
