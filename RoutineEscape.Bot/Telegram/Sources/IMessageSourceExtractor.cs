using RoutineEscape.Domain.Entities;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Sources;

public interface IMessageSourceExtractor
{
    MessageSource Extract(Message message, DateTimeOffset createdAtUtc);
}
