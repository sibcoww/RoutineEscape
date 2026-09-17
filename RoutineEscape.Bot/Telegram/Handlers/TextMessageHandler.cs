using RoutineEscape.Application.Drafts;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Bot.Telegram.Sources;
using RoutineEscape.Domain.Enums;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class TextMessageHandler(
    ITelegramBotGateway gateway,
    IMessageSourceExtractor sourceExtractor,
    MessageSourceDisplayFormatter sourceFormatter,
    IDraftFlowService draftFlowService,
    IMessageInterpreter messageInterpreter,
    TimeProvider timeProvider,
    ILogger<TextMessageHandler> logger)
{
    private const decimal HighConfidenceThreshold = 0.80m;

    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        var sender = message.From ?? throw new InvalidOperationException("Message sender is required.");
        var now = timeProvider.GetUtcNow();
        var source = sourceExtractor.Extract(message, now);
        var sourceLine = sourceFormatter.Format(source);
        MessageInterpretation? interpretation = null;
        try
        {
            interpretation = await messageInterpreter.InterpretAsync(message.Text!, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Rule interpretation failed; using manual draft flow");
        }

        var draft = await draftFlowService.CreateAsync(new CreateDraftRequest(
            sender.Id, sender.FirstName, sender.LastName, sender.Username, message.Id,
            message.Text!, source, now, interpretation), cancellationToken);
        logger.LogInformation("Recognized type={Intent} confidence={Confidence} draft={DraftId}",
            draft.SuggestedIntent, draft.Confidence, draft.DraftId);
        var highConfidence = draft.SuggestedIntent != Intent.Unknown &&
                             draft.Confidence >= HighConfidenceThreshold;
        var response = highConfidence
            ? FormatSuggestion(draft, sourceLine)
            : sourceLine is null
                ? "Не уверен, как лучше сохранить сообщение."
                : $"Не уверен, как лучше сохранить сообщение.\n{sourceLine}";
        await gateway.SendTextMessageAsync(message.Chat.Id, response, cancellationToken,
            highConfidence
                ? CreateSuggestionButtons(draft.DraftId, draft.SuggestedIntent)
                : CreateButtons(draft.DraftId));
    }

    private static string FormatSuggestion(DraftCreationResult draft, string? sourceLine)
    {
        var lines = new List<string>
        {
            $"{Icon(draft.SuggestedIntent)} Похоже на {AccusativeName(draft.SuggestedIntent)}",
            string.Empty,
            draft.SuggestedTitle!,
        };
        var when = string.Join(", ", new[] { draft.DateExpression, draft.TimeExpression }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (when.Length > 0) lines.Add(when);
        if (!string.IsNullOrWhiteSpace(draft.Location)) lines.Add($"Место: {draft.Location}");
        if (sourceLine is not null) lines.Add(sourceLine);
        return string.Join('\n', lines);
    }

    private static IReadOnlyList<IReadOnlyList<BotButton>> CreateButtons(Guid draftId) =>
    [
        [new("✅ Задача", Data("Task", draftId)), new("📅 Событие", Data("Event", draftId))],
        [new("⏰ Напоминание", Data("Reminder", draftId)), new("📝 Заметка", Data("Note", draftId))],
        [new("❌ Отмена", $"draft:cancel:{draftId:N}")],
    ];

    private static IReadOnlyList<IReadOnlyList<BotButton>> CreateSuggestionButtons(Guid draftId, Intent intent)
    {
        var otherTypes = new[] { Intent.Task, Intent.Event, Intent.Reminder, Intent.Note }
            .Where(candidate => candidate != intent)
            .Select(candidate => new BotButton(
                $"{Icon(candidate)} {DisplayName(candidate)}", Data(candidate.ToString(), draftId)))
            .ToArray();
        return
        [
            [new BotButton($"✅ Добавить как {AccusativeName(intent)}",
                Data(intent.ToString(), draftId))],
            otherTypes,
            [new BotButton("❌ Отмена", $"draft:cancel:{draftId:N}")],
        ];
    }

    private static string DisplayName(Intent intent) => intent switch
    {
        Intent.Task => "Задача",
        Intent.Event => "Событие",
        Intent.Reminder => "Напоминание",
        Intent.Note => "Заметка",
        _ => throw new ArgumentOutOfRangeException(nameof(intent)),
    };

    private static string AccusativeName(Intent intent) => intent switch
    {
        Intent.Task => "задачу", Intent.Event => "событие",
        Intent.Reminder => "напоминание", Intent.Note => "заметку",
        _ => "запись",
    };

    private static string Icon(Intent intent) => intent switch
    {
        Intent.Task => "✅",
        Intent.Event => "📅",
        Intent.Reminder => "⏰",
        Intent.Note => "📝",
        _ => "❓",
    };

    private static string Data(string intent, Guid draftId) => $"draft:type:{intent}:{draftId:N}";
}
