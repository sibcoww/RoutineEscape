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
        try
        {
            await botClient.SetMyCommands([
                new BotCommand { Command = "menu", Description = "Меню разделов" },
                new BotCommand { Command = "list", Description = "Краткая сводка" },
                new BotCommand { Command = "today", Description = "Сегодня" },
                new BotCommand { Command = "upcoming", Description = "Ближайшие 7 дней" },
                new BotCommand { Command = "overdue", Description = "Просроченные задачи и напоминания" },
                new BotCommand { Command = "done", Description = "Выполненные и подтверждённые" },
                new BotCommand { Command = "tasks", Description = "Активные задачи" },
                new BotCommand { Command = "events", Description = "События" },
                new BotCommand { Command = "reminders", Description = "Активные напоминания" },
                new BotCommand { Command = "notes", Description = "Заметки" },
                new BotCommand { Command = "search", Description = "Поиск: /search часть текста" },
                new BotCommand { Command = "all", Description = "Все актуальные записи" },
                new BotCommand { Command = "timezone", Description = "Часовой пояс" },
                new BotCommand { Command = "settings", Description = "Настройки и уведомления" },
                new BotCommand { Command = "cancel", Description = "Отмена редактирования и поиска" },
                new BotCommand { Command = "help", Description = "Справка и примеры" },
            ], cancellationToken: stoppingToken);
            logger.LogInformation("Telegram command menu registered");
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Telegram command menu registration failed; commands remain available as text");
        }
        logger.LogInformation("Telegram polling ready: webhook removed; receiving updates");

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
