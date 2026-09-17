using System.Globalization;
using System.Text.RegularExpressions;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Application.Records;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Bot.Telegram;

public static class RecordOverviewFormatter
{
    public static string Confirmation(Intent intent) => intent switch
    {
        Intent.Task => "✅ Задача создана",
        Intent.Event => "📅 Событие создано",
        Intent.Reminder => "⏰ Напоминание создано",
        Intent.Note => "📝 Заметка сохранена",
        _ => throw new ArgumentOutOfRangeException(nameof(intent)),
    };

    public static string Saved(DraftSelectionResult saved, RecordOverview overview, DateTimeOffset now)
    {
        var lines = new List<string> { Confirmation(saved.Intent),
            Line(new SavedRecord(saved.EntityId ?? Guid.Empty, saved.Intent, saved.Title, saved.AtUtc, now, saved.HasExplicitTime), overview.TimeZoneId, now) };
        if (saved.Intent == Intent.Task && !saved.HasExplicitTime)
            lines.Add("Без точного времени уведомление не запланировано. Его можно задать в карточке задачи.");
        if (overview.Items.Count > 0)
        {
            lines.Add("");
            lines.Add("Ближайшие 3 дня и свежие записи без даты:");
            lines.AddRange(overview.Items.Select(item => Line(item, overview.TimeZoneId, now)));
        }
        if (overview.RemainingCount > 0) lines.Add($"Ещё записей в полном списке: {overview.RemainingCount}.");
        lines.Add($"Часовой пояс: {overview.TimeZoneId}");
        return string.Join("\n\n", lines.Where(line => line.Length > 0));
    }

    public static string Page(RecordOverview overview, DateTimeOffset now) => overview.TotalCount == 0
        ? "Пока нет актуальных записей. Отправьте сообщение, чтобы добавить запись."
        : $"<b>Ваши актуальные записи · {overview.TotalCount}</b>\nСтраница {overview.Page + 1}/{overview.PageCount}\n\n"
          + string.Join("\n\n", overview.Items.Select(item => Line(item, overview.TimeZoneId, now)))
          + $"\n\nЧасовой пояс: {overview.TimeZoneId}";

    public static string Summary(RecordOverview overview, DateTimeOffset now)
    {
        if (overview.TotalCount == 0) return "Пока нет актуальных записей. Отправьте сообщение, чтобы добавить запись.";
        var text = overview.Items.Count == 0
            ? "На ближайшие 3 дня записей нет. Другие актуальные записи доступны по кнопке «Показать всё»."
            : "<b>Ближайшие 3 дня и свежие записи без даты:</b>\n\n" + string.Join("\n\n", overview.Items.Select(item => Line(item, overview.TimeZoneId, now)));
        if (overview.RemainingCount > 0) text += $"\nЕщё записей в полном списке: {overview.RemainingCount}.";
        return text + $"\n\nЧасовой пояс: {overview.TimeZoneId}";
    }

    public static IReadOnlyList<IReadOnlyList<BotButton>> SummaryButtons(RecordOverview overview)
    {
        var rows = ItemButtons(overview.Items);
        rows.Add([new("Показать всё", "records:page:0")]);
        rows.Add([new("Меню и поиск", "browse:menu")]);
        return rows;
    }

    public static IReadOnlyList<IReadOnlyList<BotButton>> PageButtons(RecordOverview overview)
    {
        var rows = ItemButtons(overview.Items);
        var row = new List<BotButton>();
        if (overview.Page > 0) row.Add(new("← Назад", $"records:page:{overview.Page - 1}"));
        if (overview.Page + 1 < overview.PageCount) row.Add(new("Далее →", $"records:page:{overview.Page + 1}"));
        if (row.Count > 0) rows.Add(row);
        rows.Add([new("Меню и поиск", "browse:menu")]);
        return rows;
    }

    public static IReadOnlyList<IReadOnlyList<BotButton>> SummaryButtons(DraftSelectionResult saved, RecordOverview overview)
    {
        var rows = new List<IReadOnlyList<BotButton>>();
        if (saved.EntityId is { } id) rows.Add([new("Открыть сохранённую запись", $"rec:{saved.Intent}:{id:N}:open")]);
        rows.AddRange(ItemButtons(overview.Items));
        rows.Add([new("Показать всё", "records:page:0")]);
        rows.Add([new("Меню и поиск", "browse:menu")]);
        return rows;
    }

    private static List<IReadOnlyList<BotButton>> ItemButtons(IReadOnlyList<SavedRecord> items) => items
        .Select(item => (IReadOnlyList<BotButton>)new[] { new BotButton($"Открыть: {ShortTitle(item.Title)}", $"rec:{item.Intent}:{item.Id:N}:open") }).ToList();

    public static string Line(SavedRecord item, string zoneId, DateTimeOffset now)
    {
        var label = item.Intent switch { Intent.Task => "✅ Задача", Intent.Event => "📅 Событие", Intent.Reminder => "⏰ Напоминание", _ => "📝 Заметка" };
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var when = "без даты";
        if (item.AtUtc is { } at)
        {
            var local = TimeZoneInfo.ConvertTime(at, zone);
            var today = TimeZoneInfo.ConvertTime(now, zone).Date;
            var date = local.Date == today ? "сегодня" : local.Date == today.AddDays(1) ? "завтра" : local.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
            when = item.Intent == Intent.Task && !item.HasExplicitTime && local.TimeOfDay == TimeSpan.Zero ? date : $"{date} {local:HH:mm}";
        }
        var status = RecordTiming.IsOverdue(item, zone, now) ? " · ⚠️ <b>просрочено</b>"
            : item.Status switch { "Completed" => " · выполнено", "Triggered" => " · подтверждено", "Cancelled" => " · отменено", "Past" => " · прошло", _ => "" };
        return $"{label} — <b>{Escape(ShortTitle(item.Title))}</b>\n<i>{Escape(when)}</i>{status}";
    }

    public static string Escape(string text) => System.Net.WebUtility.HtmlEncode(text);

    private static string ShortTitle(string title)
    {
        var singleLine = Regex.Replace(title, @"\s+", " ").Trim();
        if (singleLine.Length <= 80) return singleLine;
        var boundary = StringInfo.ParseCombiningCharacters(singleLine).Last(index => index <= 79);
        return singleLine[..boundary] + "…";
    }
}
