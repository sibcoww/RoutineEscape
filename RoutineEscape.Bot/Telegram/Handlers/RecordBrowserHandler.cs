using RoutineEscape.Application.Records;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class RecordBrowserHandler(IRecordBrowserService browser, ITelegramBotGateway gateway,
    RecordInteractionState interactions, SearchSessions searches, TimeProvider clock)
{
    private static readonly Dictionary<string, RecordView> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/all"] = RecordView.All, ["/today"] = RecordView.Today, ["/upcoming"] = RecordView.Upcoming,
        ["/overdue"] = RecordView.Overdue, ["/done"] = RecordView.Completed, ["/tasks"] = RecordView.Tasks,
        ["/events"] = RecordView.Events, ["/reminders"] = RecordView.Reminders, ["/notes"] = RecordView.Notes,
    };

    public static readonly IReadOnlyList<IReadOnlyList<BotButton>> Menu =
    [
        [new("Сегодня", "browse:Today:0"), new("Ближайшие 7 дней", "browse:Upcoming:0")],
        [new("Просроченные", "browse:Overdue:0"), new("Выполненные", "browse:Completed:0")],
        [new("Задачи", "browse:Tasks:0"), new("События", "browse:Events:0")],
        [new("Напоминания", "browse:Reminders:0"), new("Заметки", "browse:Notes:0")],
        [new("Все актуальные", "browse:All:0"), new("Поиск", "browse:search")],
        [new("Справка", "browse:help"), new("Настройки", "browse:settings")],
    ];
    private const string SearchHelp = "Поиск по всем своим записям, включая выполненные и прошлые события.\nОтправьте /search и часть текста, например: /search документы\nОт 1 до 100 символов, без учёта регистра. Поиск не включает режим ввода: обычный текст по-прежнему создаёт запись или редактирует выбранное поле.";
    private const string Help = "RoutineEscape — задачи, события, напоминания и заметки.\n\nОтправьте или перешлите текст и подтвердите тип. Название сохраняется без перефразирования.\nПримеры: «купить молоко», «созвон завтра в 19:00», «напомни через 40 минут выключить духовку», «заметка: номер 305».\n\n/menu — меню; /list — краткая сводка; /all — все актуальные.\n/today — сегодня; /upcoming — 7 дней; /overdue — просроченные; /done — выполненные задачи и подтверждённые напоминания.\n/tasks, /events, /reminders, /notes — по типу.\n/search текст — поиск, включая историю.\n/timezone — часовой пояс; /cancel — отмена редактирования и поиска.\n\nВ карточке: изменить, выполнить/восстановить задачу, удалить с подтверждением. Для уведомления нужно точное время. После доставки — один повтор через 10 минут; «Готово» подтверждает, перенос доступен на 10 минут или час. Периодические задачи пока не поддерживаются.";
    private const string Settings = "Настройки\n\n/timezone — посмотреть местное время и часовой пояс.\n/timezone Asia/Qyzylorda — UTC+05:00 по системной базе.\n/timezone Europe/Moscow — московская зона.\nИзменение зоны не переносит сохранённые сроки.\n\nУведомления включаются точным временем записи; у задачи удалите срок через «-», чтобы их выключить. /cancel отменяет редактирование.";

    public async Task<bool> HandleMessageAsync(Message message, CancellationToken ct)
    {
        if (message.From is null || message.Text is not { } text || !text.StartsWith('/')) return false;
        var split = text.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = split[0].Split('@')[0];
        var user = message.From.Id;
        if (Commands.TryGetValue(command, out var view)) await ShowAsync(user, view, 0, null, ct);
        else if (command.Equals("/search", StringComparison.OrdinalIgnoreCase))
        {
            var query = split.Length > 1 ? split[1].Trim() : "";
            if (query.Length is < 1 or > 100) await SendAsync(user, SearchHelp, Menu, ct);
            else await ShowAsync(user, RecordView.Search, 0, searches.Create(user, query, clock.GetUtcNow()), ct);
        }
        else if (command.Equals("/menu", StringComparison.OrdinalIgnoreCase)) await SendAsync(user, "Выберите раздел. Все даты показаны в вашем часовом поясе.", Menu, ct);
        else if (command.Equals("/help", StringComparison.OrdinalIgnoreCase) || command.Equals("/start", StringComparison.OrdinalIgnoreCase)) await SendAsync(user, Help, Menu, ct);
        else if (command.Equals("/settings", StringComparison.OrdinalIgnoreCase)) await SendAsync(user, Settings, Menu, ct);
        else if (command.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
        {
            searches.Clear(user);
            if (interactions.Get(user) is not null) return false;
            await SendAsync(user, "Поиск закрыт. Активного редактирования нет. Записи не изменены.", Menu, ct);
        }
        else if (command.Equals("/list", StringComparison.OrdinalIgnoreCase) || command.Equals("/timezone", StringComparison.OrdinalIgnoreCase)) return false;
        else await SendAsync(user, "Неизвестная команда. Откройте /help или выберите раздел. Новая запись не создавалась.", Menu, ct);
        return true;
    }

    public async Task<bool> HandleCallbackAsync(CallbackQuery callback, CancellationToken ct)
    {
        if (callback.Data?.StartsWith("browse:", StringComparison.Ordinal) != true) return false;
        var user = callback.From.Id;
        var parts = callback.Data.Split(':');
        if (parts.Length == 2 && parts[1] is "menu" or "help" or "settings" or "search")
        {
            await gateway.AnswerCallbackQueryAsync(callback.Id, null, ct);
            await SendAsync(user, parts[1] switch { "help" => Help, "settings" => Settings, "search" => SearchHelp, _ => "Выберите раздел." }, Menu, ct);
            return true;
        }
        SearchSessions.Session? session = null;
        var view = RecordView.All;
        var page = 0;
        var valid = parts.Length == 3 && Enum.TryParse(parts[1], out view) && Enum.IsDefined(view) && view != RecordView.Search && int.TryParse(parts[2], out page) && page >= 0;
        if (parts.Length == 4 && parts[1] == "query")
        {
            session = searches.Get(user, parts[2], clock.GetUtcNow());
            if (session is not null && parts[3] == "cancel")
            {
                searches.Clear(user);
                await gateway.AnswerCallbackQueryAsync(callback.Id, "Поиск закрыт.", ct);
                await SendAsync(user, "Поиск закрыт. Записи не изменены.", Menu, ct);
                return true;
            }
            valid = session is not null && int.TryParse(parts[3], out page) && page >= 0;
            view = RecordView.Search;
        }
        if (!valid)
        {
            await gateway.AnswerCallbackQueryAsync(callback.Id, "Кнопка недоступна или поиск истёк. Отправьте /search текст или /menu.", ct);
            return true;
        }
        await gateway.AnswerCallbackQueryAsync(callback.Id, null, ct);
        await ShowAsync(user, view, page, session, ct);
        return true;
    }

    private async Task ShowAsync(long user, RecordView view, int page, SearchSessions.Session? search, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var result = await browser.BrowseAsync(user, view, search?.Query, page, now, ct);
        var title = view switch
        {
            RecordView.Today => "Сегодня", RecordView.Upcoming => "Ближайшие 7 дней (включая сегодня)",
            RecordView.Overdue => "Просроченные задачи и напоминания", RecordView.Completed => "Выполненные задачи и подтверждённые напоминания",
            RecordView.Tasks => "Активные задачи", RecordView.Events => "События с сегодняшнего дня и продолжающиеся",
            RecordView.Reminders => "Активные напоминания", RecordView.Notes => "Заметки", RecordView.Search => $"Поиск: {search!.Query}", _ => "Все актуальные записи",
        };
        var text = $"<b>{RecordOverviewFormatter.Escape(title)} · {result.TotalCount}</b>\n" + (result.TotalCount == 0 ? "Записей не найдено." : $"Страница {result.Page + 1}/{result.PageCount}\n\n" + string.Join("\n\n", result.Items.Select(item => RecordOverviewFormatter.Line(item, result.TimeZoneId, now)))) + $"\n\nЧасовой пояс: {RecordOverviewFormatter.Escape(result.TimeZoneId)}";
        var buttons = RecordOverviewFormatter.PageButtons(result).Where(row => row.Any(button => button.CallbackData.StartsWith("rec:", StringComparison.Ordinal))).ToList();
        string Data(int target) => search is null ? $"browse:{view}:{target}" : $"browse:query:{search.Token}:{target}";
        var navigation = new List<BotButton>();
        if (result.Page > 0) navigation.Add(new("← Назад", Data(result.Page - 1)));
        if (result.Page + 1 < result.PageCount) navigation.Add(new("Далее →", Data(result.Page + 1)));
        if (navigation.Count > 0) buttons.Add(navigation);
        if (search is not null) buttons.Add([new("Закрыть поиск", $"browse:query:{search.Token}:cancel")]);
        buttons.Add([new("Меню", "browse:menu")]);
        await SendAsync(user, text, buttons, ct, html: true);
    }

    private Task SendAsync(long user, string text, IReadOnlyList<IReadOnlyList<BotButton>> buttons, CancellationToken ct, bool html = false)
    {
        if (interactions.Get(user) is not null)
            text += "\n\nРедактирование или подтверждение удаления остаётся открытым. Следующий обычный текст относится к нему; /cancel отменяет действие.";
        return html ? gateway.SendHtmlMessageAsync(user, text, ct, buttons) : gateway.SendTextMessageAsync(user, text, ct, buttons);
    }
}
