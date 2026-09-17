using RoutineEscape.Infrastructure.Persistence;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class NotificationCallbackHandler(NotificationProcessor processor, ITelegramBotGateway gateway, TimeProvider clock)
{
    public async Task<bool> HandleAsync(CallbackQuery callback, CancellationToken ct)
    {
        if (callback.Data?.StartsWith("notify:", StringComparison.Ordinal) != true) return false;
        var parts = callback.Data.Split(':');
        var result = parts.Length == 3 && Guid.TryParseExact(parts[2], "N", out var token)
            ? await processor.ActAsync(callback.From.Id, token, parts[1], clock.GetUtcNow(), ct) : null;
        await gateway.AnswerCallbackQueryAsync(callback.Id, result ?? "Уведомление недоступно или уже обработано.", ct);
        if (result is not null)
            await gateway.SendTextMessageAsync(callback.From.Id, result, ct, [[new("Показать всё", "records:page:0")]]);
        return true;
    }
}
