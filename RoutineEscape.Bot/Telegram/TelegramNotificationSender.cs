using RoutineEscape.Application.Records;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Bot.Telegram;

public sealed class TelegramNotificationSender(ITelegramBotGateway gateway) : INotificationSender
{
    public Task SendAsync(ScheduledNotification notification, CancellationToken ct)
    {
        var type = notification.Intent switch { Intent.Task => "Задача", Intent.Event => "Событие", _ => "Напоминание" };
        var local = TimeZoneInfo.ConvertTime(notification.AtUtc, TimeZoneInfo.FindSystemTimeZoneById(notification.TimeZoneId));
        var title = notification.Title.Length > 500 ? notification.Title[..499] + "…" : notification.Title;
        var text = $"⏰ {type} · {local:dd.MM HH:mm} ({notification.TimeZoneId})\n\n{title}\n\n"
            + (notification.IsFollowUp ? "Прошло 10 минут. Подтвердите выполнение или отложите. Это последний повтор в этом цикле."
                : "Время пришло. Если не подтвердить и не отложить, напомню ещё один раз через 10 минут.")
            + (notification.Intent == Intent.Event ? "\n«Готово» подтверждает уведомление, не завершает событие." : "");
        var token = notification.ActionToken.ToString("N");
        return gateway.SendTextMessageAsync(notification.TelegramUserId, text, ct,
            [[new("Готово", $"notify:done:{token}")], [new("Отложить на 10 минут", $"notify:10:{token}"), new("На час", $"notify:60:{token}")]]);
    }
}
