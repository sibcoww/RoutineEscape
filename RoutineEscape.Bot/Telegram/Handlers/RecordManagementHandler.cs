using RoutineEscape.Application.Records;
using RoutineEscape.Domain.Enums;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class RecordManagementHandler(IRecordManagementService records, IRecordOverviewService overview,
    RecordInteractionState states, ITelegramBotGateway gateway, TimeProvider clock, ILogger<RecordManagementHandler> logger)
{
    public async Task<bool> HandleCallbackAsync(CallbackQuery callback, CancellationToken ct)
    {
        var data = callback.Data ?? string.Empty;
        if (!data.StartsWith("rec:", StringComparison.Ordinal) && !data.StartsWith("edit:", StringComparison.Ordinal)) return false;
        var user = callback.From.Id;
        var gate = states.Gate(user); await gate.WaitAsync(ct);
        try
        {
            var parts = data.Split(':');
            if (parts[0] == "edit")
            {
                var session = states.Get(user);
                if (parts.Length != 3 || !Guid.TryParseExact(parts[2], "N", out var token) || session is null || session.Token != token)
                    throw new KeyNotFoundException();
                if (parts[1] == "cancel")
                {
                    states.Clear(user);
                    await gateway.AnswerCallbackQueryAsync(callback.Id, "Отменено. Запись не изменена.", ct);
                    await ShowCardAsync(user, session.Intent, session.RecordId, "Изменение отменено.", ct);
                    return true;
                }
                if (parts[1] != "delete" || session.Field != "delete" || session.ExpiresAt <= clock.GetUtcNow()) throw new KeyNotFoundException();
                var current = await records.GetAsync(user, session.Intent, session.RecordId, ct);
                if (RecordCardFormatter.Version(current) != session.Version) throw new KeyNotFoundException();
                await records.DeleteAsync(user, session.Intent, session.RecordId, ct);
                states.Clear(user);
                logger.LogInformation("Record deleted: type={Intent} record={RecordId}", session.Intent, session.RecordId);
                await gateway.AnswerCallbackQueryAsync(callback.Id, "Запись удалена.", ct);
                await ShowListAsync(user, "Запись удалена.", ct);
                return true;
            }

            if (parts.Length is not (4 or 5) || !Enum.TryParse<Intent>(parts[1], out var intent) || !Enum.IsDefined(intent)
                || intent == Intent.Unknown || !Guid.TryParseExact(parts[2], "N", out var id)) throw new FormatException();
            var record = await records.GetAsync(user, intent, id, ct);
            var action = parts[3];
            if (action == "open")
            {
                await gateway.AnswerCallbackQueryAsync(callback.Id, null, ct);
                await SendCardAsync(user, record, null, ct);
                return true;
            }
            if (parts.Length != 5 || !states.IsCurrentCard(user, id, parts[4], RecordCardFormatter.Version(record))) throw new KeyNotFoundException();
            if (action is "done" or "undo")
            {
                if (intent != Intent.Task) throw new FormatException();
                record = await records.SetCompletedAsync(user, id, action == "done", clock.GetUtcNow(), ct);
                states.Clear(user);
                logger.LogInformation("Task status updated: record={RecordId} status={Status}", id, record.Status);
                await gateway.AnswerCallbackQueryAsync(callback.Id, action == "done" ? "Задача выполнена." : "Задача восстановлена.", ct);
                await SendCardAsync(user, record, action == "done" ? "Задача выполнена и убрана из актуального списка." : "Задача снова в актуальном списке.", ct);
                return true;
            }
            if (action is not ("text" or "date" or "del") || action == "date" && intent == Intent.Note) throw new FormatException();
            var field = action == "del" ? "delete" : action;
            var pending = new RecordInteraction(Guid.NewGuid(), intent, id, field, RecordCardFormatter.Version(record), clock.GetUtcNow().AddMinutes(15));
            states.Set(user, pending);
            states.InvalidateCard(user, id);
            await gateway.AnswerCallbackQueryAsync(callback.Id, null, ct);
            var prompt = field == "delete" ? "Удалить эту запись? Это действие нельзя отменить.\n\n" + RecordCardFormatter.Text(record)
                : field == "text" ? $"✏️ Редактирование записи\nОтправьте {(intent == Intent.Note ? "новый текст заметки (до 3500 символов)" : "новое название (до 500 символов)")} одним сообщением. Оно заменит текущее поле."
                : $"✏️ Редактирование записи\nОтправьте новую дату и время: «завтра в 19:00», «12 сентября в 15:00» или «через 40 минут». Часовой пояс: {record.TimeZoneId}."
                    + (intent == Intent.Task ? " Для срока задачи можно указать только дату; «-» убирает срок." : " Дата и время обязательны.");
            if (field != "delete") prompt += "\nРежим действует 15 минут. /cancel или кнопка отменяют ввод. Можно ответить на это сообщение.";
            await gateway.SendTextMessageAsync(user, prompt, ct, field == "delete"
                ? [[new("Да, удалить", $"edit:delete:{pending.Token:N}"), new("Отмена", $"edit:cancel:{pending.Token:N}")]]
                : [[new("Отмена", $"edit:cancel:{pending.Token:N}")]]);
            return true;
        }
        catch (Exception error) when (error is KeyNotFoundException or FormatException or InvalidOperationException)
        {
            await gateway.AnswerCallbackQueryAsync(callback.Id, "Запись недоступна или кнопка устарела. Откройте запись заново из списка.", ct);
            return true;
        }
        finally { gate.Release(); }
    }

    public async Task<bool> HandleTextAsync(Message message, CancellationToken ct)
    {
        if (message.From is null || message.Chat.Id != message.From.Id || message.Text is null) return false;
        var user = message.From.Id;
        var gate = states.Gate(user); await gate.WaitAsync(ct);
        try
        {
            if (states.WasHandled(user, message.Id)) return true;
            var session = states.Get(user);
            if (session is null)
            {
                if (message.ReplyToMessage?.From?.IsBot == true && message.ReplyToMessage.Text?.StartsWith("✏️ Редактирование записи", StringComparison.Ordinal) == true)
                {
                    await gateway.SendTextMessageAsync(user, "Режим редактирования завершён. Откройте запись из списка и выберите изменение заново.", ct);
                    return true;
                }
                return false;
            }
            if (message.Text.Trim().Split('@')[0].Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                states.Clear(user); states.MarkHandled(user, message.Id);
                await ShowCardAsync(user, session.Intent, session.RecordId, "Отменено. Запись не изменена.", ct);
                return true;
            }
            if (session.ExpiresAt <= clock.GetUtcNow())
            {
                states.Clear(user); states.MarkHandled(user, message.Id);
                await gateway.SendTextMessageAsync(user, "Время редактирования истекло. Запись не изменена; откройте её заново из списка.", ct, [[new("К списку", "records:page:0")]]);
                return true;
            }
            if (session.Field == "delete" || message.Text.StartsWith('/'))
            {
                await gateway.SendTextMessageAsync(user, "Сначала завершите действие кнопками или отправьте /cancel. Новая запись не создавалась.", ct, [[new("Отмена", $"edit:cancel:{session.Token:N}")]]);
                return true;
            }
            var current = await records.GetAsync(user, session.Intent, session.RecordId, ct);
            if (RecordCardFormatter.Version(current) != session.Version) throw new KeyNotFoundException();
            var changed = await records.EditAsync(user, session.Intent, session.RecordId, session.Field, message.Text, clock.GetUtcNow(), ct);
            states.Clear(user); states.MarkHandled(user, message.Id);
            logger.LogInformation("Record edited: type={Intent} record={RecordId} field={Field}", session.Intent, session.RecordId, session.Field);
            await SendCardAsync(user, changed, "Изменения сохранены.", ct);
            return true;
        }
        catch (RecordInputException error)
        {
            var session = states.Get(user)!;
            await gateway.SendTextMessageAsync(user, error.Message, ct, [[new("Отмена", $"edit:cancel:{session.Token:N}")]]);
            return true;
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException)
        {
            states.Clear(user); states.MarkHandled(user, message.Id);
            await gateway.SendTextMessageAsync(user, "Запись удалена или изменена. Ввод не сохранён; откройте запись заново.", ct, [[new("К списку", "records:page:0")]]);
            return true;
        }
        finally { gate.Release(); }
    }

    private async Task ShowCardAsync(long user, Intent intent, Guid id, string prefix, CancellationToken ct) =>
        await SendCardAsync(user, await records.GetAsync(user, intent, id, ct), prefix, ct);
    private Task SendCardAsync(long user, RecordDetails record, string? prefix, CancellationToken ct)
    {
        var token = states.IssueCard(user, record.Id, RecordCardFormatter.Version(record));
        return gateway.SendTextMessageAsync(user, (prefix is null ? "" : prefix + "\n\n") + RecordCardFormatter.Text(record), ct, RecordCardFormatter.Buttons(record, token));
    }
    private async Task ShowListAsync(long user, string prefix, CancellationToken ct)
    {
        var page = await overview.PageAsync(user, 0, clock.GetUtcNow(), ct);
        await gateway.SendHtmlMessageAsync(user, prefix + "\n\n" + RecordOverviewFormatter.Page(page, clock.GetUtcNow()), ct, RecordOverviewFormatter.PageButtons(page));
    }
}
