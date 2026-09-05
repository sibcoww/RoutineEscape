using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace RoutineEscape.Bot.Telegram;

public sealed class TelegramLongPollingService(
    ITelegramBotClient botClient,
    IServiceScopeFactory scopeFactory,
    IOptions<TelegramOptions> options,
    ILogger<TelegramLongPollingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.UseLongPolling)
        {
            logger.LogInformation("Telegram long polling is disabled; webhook mode is available");
            return;
        }

        logger.LogInformation("Starting Telegram long polling");
        await botClient.DeleteWebhook(dropPendingUpdates: false, cancellationToken: stoppingToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
            DropPendingUpdates = false,
        };

        await botClient.ReceiveAsync(
            HandleUpdateAsync,
            HandlePollingErrorAsync,
            receiverOptions,
            stoppingToken);
    }

    private async Task HandleUpdateAsync(
        ITelegramBotClient _,
        Update update,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ITelegramUpdateDispatcher>();

        try
        {
            await dispatcher.DispatchAsync(update, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process polled Telegram update {UpdateId}", update.Id);
        }
    }

    private Task HandlePollingErrorAsync(
        ITelegramBotClient _,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Telegram long polling failed");
        }

        return Task.CompletedTask;
    }
}
