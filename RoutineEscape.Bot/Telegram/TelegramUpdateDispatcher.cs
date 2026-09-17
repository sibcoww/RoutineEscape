using RoutineEscape.Bot.Telegram.Handlers;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram;

public sealed class TelegramUpdateDispatcher(
    StartCommandHandler startCommandHandler,
    TextMessageHandler textMessageHandler,
    CallbackQueryHandler callbackQueryHandler,
    ILogger<TelegramUpdateDispatcher> logger,
    RecordManagementHandler? recordManagement = null,
    ListCommandHandler? listCommandHandler = null,
    TimeZoneCommandHandler? timeZoneCommandHandler = null,
    RecordBrowserHandler? recordBrowser = null) : ITelegramUpdateDispatcher
{
    public async Task DispatchAsync(Update update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        using var scope = logger.BeginScope("UpdateId={UpdateId} ProcessingId={ProcessingId}", update.Id, Guid.NewGuid().ToString("N"));
        logger.LogInformation("Update received: type={UpdateType} user={UserId} chat={ChatId} text={Text}",
            update.Type, update.Message?.From?.Id ?? update.CallbackQuery?.From.Id,
            update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id,
            update.Message?.Text ?? update.CallbackQuery?.Data);

        try
        {
            if (update.CallbackQuery is not null)
            {
                await callbackQueryHandler.HandleAsync(update.CallbackQuery, cancellationToken);
                return;
            }

            if (update.Message?.Text is not { } text)
            {
                logger.LogDebug("Telegram update {UpdateId} has no supported content", update.Id);
                return;
            }

            if (recordBrowser is not null && await recordBrowser.HandleMessageAsync(update.Message, cancellationToken)) return;
            var command = text.Split(' ', 2, StringSplitOptions.TrimEntries)[0].Split('@')[0];
            if (command.Equals("/timezone", StringComparison.OrdinalIgnoreCase) && timeZoneCommandHandler is not null)
            {
                await timeZoneCommandHandler.HandleAsync(update.Message, cancellationToken);
                return;
            }
            if (IsListCommand(text) && listCommandHandler is not null)
            {
                logger.LogInformation("Recognized command: /list");
                await listCommandHandler.HandleAsync(update.Message, cancellationToken);
                return;
            }
            if (recordManagement is not null && await recordManagement.HandleTextAsync(update.Message, cancellationToken)) return;
            if (IsStartCommand(text))
            {
                logger.LogInformation("Recognized command: /start");
                await startCommandHandler.HandleAsync(update.Message, cancellationToken);
            }
            else await textMessageHandler.HandleAsync(update.Message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Update processing failed");
            throw;
        }
        finally
        {
            logger.LogInformation("Update processing finished: elapsedMs={ElapsedMs}",
                System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static bool IsStartCommand(string text)
    {
        var command = text.Split(' ', 2, StringSplitOptions.TrimEntries)[0];
        return command.Equals("/start", StringComparison.OrdinalIgnoreCase)
            || command.StartsWith("/start@", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsListCommand(string text)
    {
        var command = text.Split(' ', 2, StringSplitOptions.TrimEntries)[0];
        return command.Equals("/list", StringComparison.OrdinalIgnoreCase)
            || command.StartsWith("/list@", StringComparison.OrdinalIgnoreCase) && command.Length > "/list@".Length;
    }
}
