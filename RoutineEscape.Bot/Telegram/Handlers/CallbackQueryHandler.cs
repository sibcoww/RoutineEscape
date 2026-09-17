using RoutineEscape.Application.Drafts;
using RoutineEscape.Domain.Enums;
using RoutineEscape.Application.Records;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class CallbackQueryHandler(
    ITelegramBotGateway gateway,
    IDraftFlowService draftFlowService,
    TimeProvider timeProvider,
    IRecordOverviewService overviewService,
    RecordManagementHandler? recordManagement = null,
    NotificationCallbackHandler? notifications = null,
    RecordBrowserHandler? recordBrowser = null,
    CallbackResponseContext? responses = null)
{
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        responses?.Begin(callbackQuery);
        if (recordBrowser is not null && await recordBrowser.HandleCallbackAsync(callbackQuery, cancellationToken)) return;
        if (notifications is not null && await notifications.HandleAsync(callbackQuery, cancellationToken)) return;
        if (recordManagement is not null && await recordManagement.HandleCallbackAsync(callbackQuery, cancellationToken)) return;
        try
        {
            if (callbackQuery.Data?.StartsWith("records:page:", StringComparison.Ordinal) == true)
            {
                if (!int.TryParse(callbackQuery.Data["records:page:".Length..], out var page) || page < 0)
                    throw new FormatException("Invalid page.");
                var overview = await overviewService.PageAsync(callbackQuery.From.Id, page, timeProvider.GetUtcNow(), cancellationToken);
                await gateway.AnswerCallbackQueryAsync(callbackQuery.Id, null, cancellationToken);
                await gateway.SendHtmlMessageAsync(callbackQuery.From.Id,
                    RecordOverviewFormatter.Page(overview, timeProvider.GetUtcNow()), cancellationToken,
                    RecordOverviewFormatter.PageButtons(overview));
                return;
            }
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
                RecordOverviewFormatter.Confirmation(result.Intent), cancellationToken);
            var summary = await overviewService.SummaryAsync(callbackQuery.From.Id, timeProvider.GetUtcNow(), result.EntityId, cancellationToken);
            await gateway.SendHtmlMessageAsync(callbackQuery.From.Id,
                RecordOverviewFormatter.Saved(result, summary, timeProvider.GetUtcNow()), cancellationToken,
                RecordOverviewFormatter.SummaryButtons(result, summary));
        }
        catch (DraftDateValidationException exception)
        {
            await gateway.AnswerCallbackQueryAsync(callbackQuery.Id, exception.Message, cancellationToken);
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
            && Enum.TryParse<Intent>(parts[2], true, out var intent) && Enum.IsDefined(intent) && intent != Intent.Unknown
            && Guid.TryParseExact(parts[3], "N", out var draftId))
        {
            return new DraftCallbackAction(draftId, intent, false);
        }

        throw new FormatException("Callback data is invalid.");
    }

    private sealed record DraftCallbackAction(Guid DraftId, Intent? Intent, bool IsCancellation);
}
