using Telegram.Bot.Types;
using RoutineEscape.Bot.Telegram.Sources;
using RoutineEscape.Application.Drafts;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class TextMessageHandler(
    ITelegramBotGateway gateway,
    IMessageSourceExtractor sourceExtractor,
    MessageSourceDisplayFormatter sourceFormatter,
    IDraftFlowService draftFlowService,
    TimeProvider timeProvider)
{
    public Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        return HandleCoreAsync(message, cancellationToken);
    }

    private async Task HandleCoreAsync(Message message, CancellationToken cancellationToken)
    {
        var sender = message.From ?? throw new InvalidOperationException("Message sender is required.");
        var now = timeProvider.GetUtcNow();
        var source = sourceExtractor.Extract(message, now);
        var sourceLine = sourceFormatter.Format(source);
        var draft = await draftFlowService.CreateAsync(new CreateDraftRequest(
            sender.Id, sender.FirstName, sender.LastName, sender.Username, message.Id,
            message.Text!, source, now), cancellationToken);
        var response = sourceLine is null
            ? "Как сохранить сообщение?"
            : $"Как сохранить сообщение?\n{sourceLine}";
        await gateway.SendTextMessageAsync(message.Chat.Id, response, cancellationToken,
            CreateButtons(draft.DraftId));
    }

    private static IReadOnlyList<IReadOnlyList<BotButton>> CreateButtons(Guid draftId) =>
    [
        [new("✅ Задача", Data("Task", draftId)), new("📅 Событие", Data("Event", draftId))],
        [new("⏰ Напоминание", Data("Reminder", draftId)), new("📝 Заметка", Data("Note", draftId))],
        [new("❌ Отмена", $"draft:cancel:{draftId:N}")],
    ];

    private static string Data(string intent, Guid draftId) => $"draft:type:{intent}:{draftId:N}";
}
