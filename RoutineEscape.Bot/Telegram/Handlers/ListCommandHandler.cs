using RoutineEscape.Application.Records;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class ListCommandHandler(IRecordOverviewService overviewService, ITelegramBotGateway gateway,
    RecordInteractionState interactions, TimeProvider clock)
{
    public async Task HandleAsync(Message message, CancellationToken ct)
    {
        if (message.From is null) return;
        var user = message.From.Id;
        var now = clock.GetUtcNow();
        var overview = await overviewService.SummaryAsync(user, now, null, ct);
        var text = RecordOverviewFormatter.Summary(overview, now);
        var pending = interactions.Get(user);
        if (pending is not null)
            text += pending.ExpiresAt <= now
                ? "\n\nПредыдущий режим изменения истёк. Отправьте /cancel, затем откройте запись заново."
                : pending.Field == "delete"
                    ? "\n\nПодтверждение удаления ещё ожидается. Вернитесь к его кнопкам или отправьте /cancel."
                    : "\n\nРедактирование остаётся открытым. Следующее сообщение изменит выбранное поле; /cancel отменяет ввод.";
        await gateway.SendHtmlMessageAsync(user, text, ct, RecordOverviewFormatter.SummaryButtons(overview));
    }
}
