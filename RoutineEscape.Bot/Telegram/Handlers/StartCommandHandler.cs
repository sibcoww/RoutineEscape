using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class StartCommandHandler(ITelegramBotGateway gateway)
{
    public Task HandleAsync(Message message, CancellationToken cancellationToken) =>
        gateway.SendTextMessageAsync(
            message.Chat.Id,
            "Привет! Я RoutineEscape. Отправь или перешли мне сообщение, и я помогу превратить его в задачу, событие, напоминание или заметку.\n\n/list — актуальные записи.\n/timezone — проверить или изменить часовой пояс.\n/cancel — отменить редактирование.\nДля уведомления укажите точное время: «завтра в 18:00». Будет один повтор через 10 минут, если не нажать «Готово» или не отложить.",
            cancellationToken);
}
