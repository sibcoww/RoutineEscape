using System.Security.Cryptography;
using System.Text;
using RoutineEscape.Application.Records;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Bot.Telegram;

public static class RecordCardFormatter
{
    public static string Version(RecordDetails record) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{record.Id}|{record.Intent}|{record.Text}|{record.AtUtc:O}|{record.Status}|{record.HasExplicitTime}")))[..10];

    public static string Text(RecordDetails record)
    {
        var type = record.Intent switch { Intent.Task => "✅ Задача", Intent.Event => "📅 Событие", Intent.Reminder => "⏰ Напоминание", _ => "📝 Заметка" };
        var status = record.Status switch { "Completed" => "Выполнена", "Cancelled" => "Отменена", "Triggered" => "Подтверждено", _ => "Активна" };
        var local = record.AtUtc is { } at ? TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById(record.TimeZoneId)) : (DateTimeOffset?)null;
        var date = local is { } value ? value.ToString(record.Intent == Intent.Task && !record.HasExplicitTime && value.TimeOfDay == TimeSpan.Zero ? "dd.MM.yyyy" : "dd.MM.yyyy HH:mm") : "без даты";
        var text = record.Text.Length <= 3500 ? record.Text : record.Text[..3499] + "…";
        var notice = record.Intent == Intent.Task && !record.HasExplicitTime ? "\nУведомление выключено: задайте точное время в поле срока." : "";
        return $"{type} · {status}\n{date} · {record.TimeZoneId}{notice}\n\n{text}";
    }

    public static IReadOnlyList<IReadOnlyList<BotButton>> Buttons(RecordDetails record, string token)
    {
        var rows = new List<IReadOnlyList<BotButton>>();
        if (record.Intent == Intent.Task && record.Status is "Pending" or "Completed")
            rows.Add([Action(record, record.Status == "Completed" ? "↩️ Вернуть в задачи" : "✅ Выполнено", record.Status == "Completed" ? "undo" : "done", token)]);
        rows.Add([Action(record, record.Intent == Intent.Note ? "✏️ Изменить текст" : "✏️ Изменить название", "text", token)]);
        if (record.Intent is Intent.Task or Intent.Event || record.Intent == Intent.Reminder && record.Status == "Pending")
            rows.Add([Action(record, record.Intent == Intent.Task ? "📅 Изменить срок" : "📅 Изменить дату и время", "date", token)]);
        rows.Add([Action(record, "🗑 Удалить", "del", token)]);
        rows.Add([new("К списку", "records:page:0"), new("Меню", "browse:menu")]);
        return rows;
    }

    private static BotButton Action(RecordDetails record, string label, string action, string token) => new(label, $"rec:{record.Intent}:{record.Id:N}:{action}:{token}");
}
