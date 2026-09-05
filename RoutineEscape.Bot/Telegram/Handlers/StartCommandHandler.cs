using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class StartCommandHandler(ITelegramBotGateway gateway)
{
    public Task HandleAsync(Message message, CancellationToken cancellationToken) =>
        gateway.SendTextMessageAsync(
            message.Chat.Id,
            "Привет! Я RoutineEscape. Отправь или перешли мне сообщение, и я помогу превратить его в задачу, событие, напоминание или заметку.",
            cancellationToken);
}
