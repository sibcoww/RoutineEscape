using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;

namespace RoutineEscape.Bot.Telegram;

public sealed class TelegramBotGateway(ITelegramBotClient botClient, ILogger<TelegramBotGateway> logger,
    CallbackResponseContext? responses = null) : ITelegramBotGateway
{
    public Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken,
        IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null) => SendAsync(chatId, text, ParseMode.None, cancellationToken, buttons);

    public Task SendHtmlMessageAsync(long chatId, string html, CancellationToken cancellationToken,
        IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null) => SendAsync(chatId, html, ParseMode.Html, cancellationToken, buttons);

    private async Task SendAsync(long chatId, string text, ParseMode parseMode, CancellationToken cancellationToken,
        IReadOnlyList<IReadOnlyList<BotButton>>? buttons)
    {
        InlineKeyboardMarkup? markup = buttons is null
            ? null
            : new InlineKeyboardMarkup(buttons.Select(row => row.Select(button =>
                InlineKeyboardButton.WithCallbackData(button.Text, button.CallbackData))));
        logger.LogInformation("Response prepared: method=sendMessage chat={ChatId} text={Text} buttons={Buttons}",
            chatId, text, string.Join(" | ", buttons?.SelectMany(row => row).Select(button => button.Text) ?? []));
        try
        {
            var sent = await botClient.SendMessage(chatId, text, parseMode: parseMode, replyMarkup: markup, cancellationToken: cancellationToken);
            logger.LogInformation("Telegram accepted response: method=sendMessage chat={ChatId} message={MessageId}", chatId, sent.Id);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Telegram response failed: method=sendMessage chat={ChatId} errorCode={ErrorCode}",
                chatId, (exception as global::Telegram.Bot.Exceptions.ApiRequestException)?.ErrorCode);
            throw;
        }
        // Only remove the clicked bot message after Telegram accepted its replacement.
        // A failed delete must not turn a successful state-changing action into a retry.
        if (responses?.TakePrevious(chatId) is { } previous)
        {
            try
            {
                await botClient.DeleteMessage(chatId, previous, cancellationToken);
                logger.LogInformation("Previous bot response deleted: chat={ChatId} message={MessageId}", chatId, previous);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Previous bot response could not be deleted: chat={ChatId} message={MessageId}", chatId, previous);
            }
        }
    }

    public async Task AnswerCallbackQueryAsync(string callbackQueryId, string? text,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Response prepared: method=answerCallbackQuery callback={CallbackId} text={Text}", callbackQueryId, text);
        try
        {
            await botClient.AnswerCallbackQuery(callbackQueryId, text, cancellationToken: cancellationToken);
            logger.LogInformation("Telegram accepted response: method=answerCallbackQuery callback={CallbackId}", callbackQueryId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Telegram response failed: method=answerCallbackQuery callback={CallbackId} errorCode={ErrorCode}",
                callbackQueryId, (exception as global::Telegram.Bot.Exceptions.ApiRequestException)?.ErrorCode);
            throw;
        }
    }
}
