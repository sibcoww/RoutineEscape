using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram;

public interface ITelegramUpdateDispatcher
{
    Task DispatchAsync(Update update, CancellationToken cancellationToken);
}
