using Microsoft.EntityFrameworkCore;
using RoutineEscape.Application.DateTimeResolution;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Application.Records;
using RoutineEscape.Bot.Telegram;
using RoutineEscape.Bot.Telegram.Handlers;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;
using RoutineEscape.Infrastructure.Persistence;
using Telegram.Bot.Types;

namespace RoutineEscape.IntegrationTests.Records;

public sealed class RecordOverviewTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-16T18:30:00Z");

    [Fact]
    public async Task Summary_RespectsOwnerLocalCalendarDaysAndActiveStatuses()
    {
        await using var db = Context();
        var owner = User(123); var other = User(456);
        db.Users.AddRange(owner, other);
        db.Tasks.AddRange(Task(owner, "today-local", "2026-09-15T19:00:00Z"),
            Task(owner, "day-two", "2026-09-18T18:59:00Z"), Task(owner, "outside-window", "2026-09-18T19:00:00Z"),
            Task(other, "foreign-task", null), Task(owner, "overdue", "2026-09-15T18:59:00Z"));
        var completed = Task(owner, "completed", null); completed.Complete(Now);
        db.Tasks.Add(completed);
        var cancelled = Task(owner, "cancelled", null); cancelled.Cancel(); db.Tasks.Add(cancelled);
        var reminder = new Reminder(Guid.NewGuid(), owner.Id, "cancelled-reminder", Now.AddHours(1), Now); reminder.Cancel();
        db.Reminders.Add(reminder);
        db.Reminders.Add(new Reminder(Guid.NewGuid(), other.Id, "foreign-reminder", Now.AddHours(1), Now));
        db.Events.Add(new CalendarEvent(Guid.NewGuid(), other.Id, "foreign-event", Now.AddHours(1), Now));
        db.Notes.AddRange(new Note(Guid.NewGuid(), owner.Id, "fresh-note", Now), new Note(Guid.NewGuid(), other.Id, "foreign-note", Now));
        await db.SaveChangesAsync();
        var summary = await Service(db).SummaryAsync(123, Now, null, default);
        Assert.Equal(new[] { "today-local", "day-two", "fresh-note" }, summary.Items.Select(item => item.Title));
        Assert.Equal(2, summary.RemainingCount); // overdue and beyond three days remain available in the full list.
        var page = await Service(db).PageAsync(123, 0, Now, default);
        Assert.Equal(5, page.TotalCount);
        Assert.DoesNotContain(page.Items, item => item.Title.StartsWith("foreign") || item.Title is "completed" or "cancelled");
    }

    [Fact]
    public async Task Summary_LimitsToFiveReservesFreshNotesAndExcludesSavedRecord()
    {
        await using var db = Context(); var owner = User(123); db.Users.Add(owner);
        for (var i = 0; i < 8; i++) db.Tasks.Add(Task(owner, $"dated-{i}", Now.AddMinutes(i).ToString("O")));
        for (var i = 0; i < 4; i++) db.Notes.Add(new Note(Guid.NewGuid(), owner.Id, $"note-{i}", Now.AddMinutes(i)));
        var saved = new Note(Guid.NewGuid(), owner.Id, "just-saved", Now.AddMinutes(5)); db.Notes.Add(saved);
        await db.SaveChangesAsync();
        var summary = await Service(db).SummaryAsync(123, Now, saved.Id, default);
        Assert.Equal(new[] { "dated-0", "dated-1", "dated-2", "note-3", "note-2" }, summary.Items.Select(item => item.Title));
        Assert.Equal(7, summary.RemainingCount);
        Assert.Equal(5, summary.Items.Select(item => item.Id).Distinct().Count());
        var text = RecordOverviewFormatter.Saved(new DraftSelectionResult(Intent.Note, "just-saved", saved.Id), summary, Now);
        Assert.Equal(1, text.Split("just-saved").Length - 1);
        Assert.Contains("Ещё записей", text);
    }

    [Fact]
    public async Task Pages_AreBoundedStableAndDisjoint_AndUnknownUserSeesNothing()
    {
        await using var db = Context(); var owner = User(123); db.Users.Add(owner);
        for (var i = 0; i < 12; i++) db.Notes.Add(new Note(Guid.NewGuid(), owner.Id, $"note-{i}", Now.AddMinutes(i)));
        await db.SaveChangesAsync(); var service = Service(db);
        var pages = new List<RecordOverview>();
        for (var i = 0; i < 3; i++) pages.Add(await service.PageAsync(123, i, Now, default));
        Assert.Equal(new[] { 5, 5, 2 }, pages.Select(page => page.Items.Count));
        Assert.Equal(12, pages.SelectMany(page => page.Items).Select(item => item.Id).Distinct().Count());
        Assert.Equal(pages[1].Items, (await service.PageAsync(123, 1, Now, default)).Items);
        Assert.Equal(2, (await service.PageAsync(123, int.MaxValue, Now, default)).Page);
        Assert.Empty((await service.PageAsync(999, 0, Now, default)).Items);
        await Assert.ThrowsAsync<FormatException>(() => service.PageAsync(123, -1, Now, default));
        Assert.Single(RecordOverviewFormatter.PageButtons(pages[0]).SelectMany(row => row), button => button.CallbackData.StartsWith("records:page:"));
        Assert.Equal(2, RecordOverviewFormatter.PageButtons(pages[1]).SelectMany(row => row).Count(button => button.CallbackData.StartsWith("records:page:")));
    }

    [Theory]
    [InlineData("купить молоко", Intent.Task, "Задача создана")]
    [InlineData("созвон завтра в 19:00", Intent.Event, "Событие создано")]
    [InlineData("напомни через 40 минут выключить духовку", Intent.Reminder, "Напоминание создано")]
    [InlineData("заметка: книга", Intent.Note, "Заметка сохранена")]
    public async Task ConfirmEachType_SavesAndSendsCompactOverviewToOwner(string text, Intent intent, string confirmation)
    {
        await using var db = Context(); var owner = User(123); db.Users.Add(owner); await db.SaveChangesAsync();
        var drafts = Drafts(db); var rules = await new RuleBasedMessageInterpreter().InterpretAsync(text, default);
        var draft = await drafts.CreateAsync(new CreateDraftRequest(123, "Owner", null, null, 42, text,
            MessageSource.Direct(Guid.NewGuid(), 123, Now), Now, rules), default);
        var gateway = new Gateway(); var handler = new CallbackQueryHandler(gateway, drafts, new Clock(), Service(db));
        await handler.HandleAsync(new CallbackQuery { Id = "cb", From = new User { Id = 123, FirstName = "Owner" },
            ChatInstance = "x", Data = $"draft:type:{intent}:{draft.DraftId:N}" }, default);
        var message = Assert.Single(gateway.Messages);
        Assert.Equal(123, message.Chat);
        Assert.Contains(confirmation, message.Text);
        Assert.Contains(text, message.Text);
        Assert.Equal(1, message.Text.Split(text).Length - 1);
        Assert.Contains(message.Buttons!.SelectMany(row => row), button => button.CallbackData == "records:page:0");
        Assert.Equal(DraftStatus.Confirmed, (await db.Drafts.SingleAsync()).Status);
        Assert.Equal(intent, Assert.Single((await Service(db).PageAsync(123, 0, Now, default)).Items).Intent);
    }

    [Fact]
    public async Task PageCallback_UsesSenderRatherThanChatOrAnotherOwner_AndLimitsText()
    {
        await using var db = Context(); var owner = User(123); var other = User(456); db.Users.AddRange(owner, other);
        for (var i = 0; i < 6; i++) db.Notes.Add(new Note(Guid.NewGuid(), owner.Id, new string('x', 2000) + i, Now.AddMinutes(i)));
        db.Notes.Add(new Note(Guid.NewGuid(), other.Id, "private-other-user", Now)); await db.SaveChangesAsync();
        var gateway = new Gateway(); var handler = new CallbackQueryHandler(gateway, Drafts(db), new Clock(), Service(db));
        await handler.HandleAsync(new CallbackQuery { Id = "cb", From = new User { Id = 123, FirstName = "Owner" },
            ChatInstance = "x", Message = new Message { Chat = new Chat { Id = -999 } }, Data = "records:page:0" }, default);
        var message = Assert.Single(gateway.Messages);
        Assert.Equal(123, message.Chat);
        Assert.True(message.Text.Length < 1500);
        Assert.DoesNotContain("private-other-user", message.Text);
        Assert.Contains("Страница 1/2", message.Text);
    }

    private static RoutineEscapeDbContext Context() => new(new DbContextOptionsBuilder<RoutineEscapeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AppUser User(long id) => new(Guid.NewGuid(), id, "Owner", null, null, Now.AddDays(-10), "Asia/Qyzylorda");
    private static TaskItem Task(AppUser owner, string title, string? at) => new(Guid.NewGuid(), owner.Id, title, Now.AddDays(-10), deadlineUtc: at is null ? null : DateTimeOffset.Parse(at));
    private static RecordOverviewService Service(RoutineEscapeDbContext db) => new(new EfRepository<AppUser>(db), new EfRepository<TaskItem>(db), new EfRepository<CalendarEvent>(db), new EfRepository<Reminder>(db), new EfRepository<Note>(db));
    private static DraftFlowService Drafts(RoutineEscapeDbContext db) => new(new EfRepository<AppUser>(db), new EfRepository<MessageSource>(db), new EfRepository<Draft>(db), new EfRepository<TaskItem>(db), new EfRepository<CalendarEvent>(db), new EfRepository<Reminder>(db), new EfRepository<Note>(db), new RussianDateTimeResolver(), db);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Gateway : ITelegramBotGateway
    {
        public List<(long Chat, string Text, IReadOnlyList<IReadOnlyList<BotButton>>? Buttons)> Messages { get; } = [];
        public System.Threading.Tasks.Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken, IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null)
        { Messages.Add((chatId, text, buttons)); return System.Threading.Tasks.Task.CompletedTask; }
        public System.Threading.Tasks.Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
    }
}
