using RoutineEscape.Application.Drafts;
using RoutineEscape.Domain.Enums;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class CallbackQueryHandler(
    ITelegramBotGateway gateway,
    IDraftFlowService draftFlowService,
    TimeProvider timeProvider)
{
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        try
        {
            var action = Parse(callbackQuery.Data);
            if (action.IsCancellation)
            {
                await draftFlowService.CancelAsync(action.DraftId, callbackQuery.From.Id, cancellationToken);
                await gateway.AnswerCallbackQueryAsync(callbackQuery.Id, "Отменено.", cancellationToken);
                return;
            }

            var result = await draftFlowService.SelectTypeAsync(action.DraftId, callbackQuery.From.Id,
                action.Intent!.Value, timeProvider.GetUtcNow(), cancellationToken);
            await gateway.AnswerCallbackQueryAsync(callbackQuery.Id,
                $"{DisplayName(result.Intent)} создано.", cancellationToken);
        }
        catch (Exception exception) when (exception is FormatException
            or KeyNotFoundException or UnauthorizedAccessException or InvalidOperationException)
        {
            await gateway.AnswerCallbackQueryAsync(callbackQuery.Id,
                "Действие недоступно или устарело.", cancellationToken);
        }
    }

    private static DraftCallbackAction Parse(string? data)
    {
        var parts = data?.Split(':') ?? [];
        if (parts.Length == 3 && parts[0] == "draft" && parts[1] == "cancel"
            && Guid.TryParseExact(parts[2], "N", out var cancelledDraftId))
        {
            return new DraftCallbackAction(cancelledDraftId, null, true);
        }

        if (parts.Length == 4 && parts[0] == "draft" && parts[1] == "type"
            && Enum.TryParse<Intent>(parts[2], true, out var intent) && intent != Intent.Unknown
            && Guid.TryParseExact(parts[3], "N", out var draftId))
        {
            return new DraftCallbackAction(draftId, intent, false);
        }

        throw new FormatException("Callback data is invalid.");
    }

    private static string DisplayName(Intent intent) => intent switch
    {
        Intent.Task => "Задача",
        Intent.Event => "Событие",
        Intent.Reminder => "Напоминание",
        Intent.Note => "Заметка",
        _ => throw new ArgumentOutOfRangeException(nameof(intent)),
    };

    private sealed record DraftCallbackAction(Guid DraftId, Intent? Intent, bool IsCancellation);
}
