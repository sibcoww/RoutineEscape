namespace RoutineEscape.Bot.Telegram;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";
    public string BotToken { get; init; } = string.Empty;
    public string WebhookSecret { get; init; } = string.Empty;
    public bool UseLongPolling { get; init; }
}
