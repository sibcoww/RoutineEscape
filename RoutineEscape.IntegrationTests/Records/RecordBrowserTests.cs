using Microsoft.EntityFrameworkCore;
using RoutineEscape.Application.Records;
using RoutineEscape.Bot.Telegram;
using RoutineEscape.Bot.Telegram.Handlers;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;
using RoutineEscape.Infrastructure.Persistence;
using Telegram.Bot.Types;

namespace RoutineEscape.IntegrationTests.Records;

public sealed class RecordBrowserTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-16T18:30:00Z"); // 23:30 local

    [Theory]
    [InlineData(RecordView.Today, "date-only,timed,ongoing")]
    [InlineData(RecordView.Upcoming, "date-only,timed,ongoing,day-seven")]
    [InlineData(RecordView.Overdue, "timed,old,reminder")]
    [InlineData(RecordView.Completed, "done,confirmed")]
    [InlineData(RecordView.Tasks, "date-only,timed,old,day-seven,outside")]
    [InlineData(RecordView.Events, "ongoing")]
    [InlineData(RecordView.Reminders, "reminder")]
    [InlineData(RecordView.Notes, "note")]
    public async Task Views_RespectOwnerStatusLocalMidnightAndTimePrecision(RecordView view, string expected)
    {
        await using var db = Context();
        var owner = User(123); var other = User(456); db.Users.AddRange(owner, other);
        TaskItem Task(string name, string date, bool time = true) => new(Guid.NewGuid(), owner.Id, name, Now.AddDays(-10), deadlineUtc: DateTimeOffset.Parse(date), hasExplicitTime: time);
        db.Tasks.AddRange(Task("date-only", "2026-09-15T19:00:00Z", false), Task("timed", "2026-09-16T18:00:00Z"),
            Task("old", "2026-09-14T19:00:00Z", false), Task("day-seven", "2026-09-22T18:59:00Z"), Task("outside", "2026-09-22T19:00:00Z"));
        var done = Task("done", "2026-09-14T19:00:00Z"); done.Complete(Now); db.Tasks.Add(done);
        var cancelled = Task("cancelled", "2026-09-14T19:00:00Z"); cancelled.Cancel(); db.Tasks.Add(cancelled);
        db.Tasks.Add(new TaskItem(Guid.NewGuid(), other.Id, "foreign", Now, deadlineUtc: Now.AddMinutes(-1), hasExplicitTime: true));
        db.Events.AddRange(new CalendarEvent(Guid.NewGuid(), owner.Id, "ongoing", Now.AddDays(-2), Now, Now.AddDays(1)),
            new CalendarEvent(Guid.NewGuid(), owner.Id, "past-event", Now.AddDays(-4), Now));
        db.Reminders.Add(new Reminder(Guid.NewGuid(), owner.Id, "reminder", Now.AddDays(-1), Now));
        var confirmed = new Reminder(Guid.NewGuid(), owner.Id, "confirmed", Now.AddHours(-2), Now.AddDays(-1)); confirmed.MarkTriggered(Now); db.Reminders.Add(confirmed);
        db.Notes.Add(new Note(Guid.NewGuid(), owner.Id, "note", Now));
        await db.SaveChangesAsync();
        var page = await Service(db).BrowseAsync(123, view, null, 0, Now, default);
        Assert.Equal(expected.Split(',').Order(), page.Items.Select(item => item.Title).Order());
        Assert.Empty((await Service(db).BrowseAsync(999, view, null, 0, Now, default)).Items);
        Assert.Empty(db.Drafts);
        if (view == RecordView.Overdue) Assert.All(page.Items, item => Assert.Contains("просрочено", RecordOverviewFormatter.Line(item, page.TimeZoneId, Now)));
        if (view == RecordView.Today) Assert.DoesNotContain("просрочено", RecordOverviewFormatter.Line(page.Items.Single(item => item.Title == "date-only"), page.TimeZoneId, Now));
    }

    [Fact]
    public async Task Search_IncludesHistoryAndFullNoteContent_TreatsWildcardsLiterally()
    {
        await using var db = Context(); var owner = User(123); var other = User(456); db.Users.AddRange(owner, other);
        var task = new TaskItem(Guid.NewGuid(), owner.Id, "Документы готовы", Now); task.Complete(Now); db.Tasks.Add(task);
        db.Notes.Add(new Note(Guid.NewGuid(), owner.Id, "старые ДОКУМЕНТЫ %_", Now, title: "Папка"));
        db.Notes.Add(new Note(Guid.NewGuid(), other.Id, "документы %_ чужие", Now));
        db.Events.Add(new CalendarEvent(Guid.NewGuid(), owner.Id, "документы вчера", Now.AddDays(-1), Now));
        await db.SaveChangesAsync(); var service = Service(db);
        var result = await service.BrowseAsync(123, RecordView.Search, "  Документы  ", 0, Now, default);
        Assert.Equal(3, result.TotalCount);
        Assert.Contains(result.Items, item => item.Status == "Completed");
        Assert.Contains(result.Items, item => item.Status == "Past");
        Assert.Equal("Папка", Assert.Single((await service.BrowseAsync(123, RecordView.Search, "%_", 0, Now, default)).Items).Title);
        Assert.Empty((await service.BrowseAsync(999, RecordView.Search, "документы", 0, Now, default)).Items);
        foreach (var input in new[] { " ", new string('a', 101) })
            await Assert.ThrowsAsync<FormatException>(() => service.BrowseAsync(123, RecordView.Search, input, 0, Now, default));
        await Assert.ThrowsAsync<FormatException>(() => service.BrowseAsync(123, RecordView.All, null, -1, Now, default));
        await Assert.ThrowsAsync<FormatException>(() => service.BrowseAsync(123, (RecordView)999, null, 0, Now, default));
    }

    [Fact]
    public async Task SearchPages_StayBoundedDisjointAndRecalculateAfterDeletion()
    {
        await using var db = Context(); var owner = User(123); db.Users.Add(owner);
        for (var i = 0; i < 12; i++) db.Notes.Add(new Note(Guid.NewGuid(), owner.Id, $"Запись {i}", Now.AddMinutes(i)));
        await db.SaveChangesAsync(); var service = Service(db);
        var pages = new List<RecordOverview>();
        for (var i = 0; i < 3; i++) pages.Add(await service.BrowseAsync(123, RecordView.Search, "запись", i, Now, default));
        Assert.Equal(new[] { 5, 5, 2 }, pages.Select(item => item.Items.Count));
        Assert.Equal(12, pages.SelectMany(item => item.Items).Select(item => item.Id).Distinct().Count());
        db.Notes.RemoveRange(db.Notes); await db.SaveChangesAsync();
        var empty = await service.BrowseAsync(123, RecordView.Search, "запись", int.MaxValue, Now, default);
        Assert.Equal(0, empty.Page); Assert.Empty(empty.Items);
    }

    [Fact]
    public async Task SearchCallbacks_AreOwnerBoundExpiringCancellableAndPreserveEditing()
    {
        await using var db = Context(); var owner = User(123); db.Users.Add(owner);
        for (var i = 0; i < 7; i++) db.Notes.Add(new Note(Guid.NewGuid(), owner.Id, $"личное {i}", Now.AddMinutes(i)));
        await db.SaveChangesAsync();
        var gateway = new Gateway(); var clock = new Clock(); var state = new RecordInteractionState(); var searches = new SearchSessions();
        var pending = new RecordInteraction(Guid.NewGuid(), Intent.Note, db.Notes.First().Id, "text", "version", Now.AddMinutes(15)); state.Set(123, pending);
        var handler = new RecordBrowserHandler(Service(db), gateway, state, searches, clock);
        Assert.True(await handler.HandleMessageAsync(Message("/search@RoutineEscape_bot личное", -999), default));
        var first = Assert.Single(gateway.Messages); Assert.Equal(123, first.Chat);
        Assert.Contains("остаётся открытым", first.Text); Assert.Equal(pending, state.Get(123));
        Assert.Equal(5, first.Buttons.SelectMany(row => row).Count(button => button.CallbackData.StartsWith("rec:")));
        var next = first.Buttons.SelectMany(row => row).Single(button => button.Text == "Далее →").CallbackData;
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(next) <= 64);
        await handler.HandleCallbackAsync(Callback(next, 456), default);
        Assert.Single(gateway.Messages); Assert.Contains("недоступна", gateway.Answers.Last()!);
        await handler.HandleCallbackAsync(Callback(next), default);
        Assert.Contains("Страница 2/2", gateway.Messages.Last().Text);
        var cancel = first.Buttons.SelectMany(row => row).Single(button => button.Text == "Закрыть поиск").CallbackData;
        await handler.HandleCallbackAsync(Callback(cancel), default);
        var count = gateway.Messages.Count;
        await handler.HandleCallbackAsync(Callback(next), default); Assert.Equal(count, gateway.Messages.Count);
        Assert.Equal(pending, state.Get(123)); // Search cancellation never cancels an unrelated edit.
        await handler.HandleMessageAsync(Message("/search личное"), default);
        var expired = gateway.Messages.Last().Buttons.SelectMany(row => row).Single(button => button.Text == "Далее →").CallbackData;
        clock.Now = Now.AddMinutes(31);
        count = gateway.Messages.Count;
        await handler.HandleCallbackAsync(Callback(expired), default); Assert.Equal(count, gateway.Messages.Count);
        await handler.HandleCallbackAsync(Callback("browse:Search:0"), default);
        await handler.HandleCallbackAsync(Callback("browse:Tasks:-1"), default);
        Assert.Equal(count, gateway.Messages.Count);
        Assert.Empty(db.Drafts);
    }

    [Fact]
    public void NewSearchInvalidatesPreviousButtons_AndRestartRequiresNewQuery()
    {
        var sessions = new SearchSessions();
        var old = sessions.Create(123, "old", Now);
        var fresh = sessions.Create(123, "new", Now);
        Assert.Null(sessions.Get(123, old.Token, Now));
        Assert.Null(sessions.Get(456, fresh.Token, Now));
        Assert.Null(new SearchSessions().Get(123, fresh.Token, Now));
        Assert.Equal("new", sessions.Get(123, fresh.Token, Now)!.Query);
        sessions.Clear(123); Assert.Null(sessions.Get(123, fresh.Token, Now));
    }

    private static RoutineEscapeDbContext Context() => new(new DbContextOptionsBuilder<RoutineEscapeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AppUser User(long id) => new(Guid.NewGuid(), id, "Owner", null, null, Now, "Asia/Qyzylorda");
    private static RecordOverviewService Service(RoutineEscapeDbContext db) => new(new EfRepository<AppUser>(db), new EfRepository<TaskItem>(db), new EfRepository<CalendarEvent>(db), new EfRepository<Reminder>(db), new EfRepository<Note>(db));
    private static Message Message(string text, long chat = 123) => new() { Id = 1, Text = text, Chat = new Chat { Id = chat }, From = new User { Id = 123, FirstName = "Owner" } };
    private static CallbackQuery Callback(string data, long user = 123) => new() { Id = "cb", Data = data, From = new User { Id = user, FirstName = "Owner" }, ChatInstance = "x" };
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = RecordBrowserTests.Now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Gateway : ITelegramBotGateway
    {
        public List<(long Chat, string Text, IReadOnlyList<IReadOnlyList<BotButton>> Buttons)> Messages { get; } = [];
        public List<string?> Answers { get; } = [];
        public Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken, IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null) { Messages.Add((chatId, text, buttons ?? [])); return Task.CompletedTask; }
        public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, CancellationToken cancellationToken) { Answers.Add(text); return Task.CompletedTask; }
    }
}
