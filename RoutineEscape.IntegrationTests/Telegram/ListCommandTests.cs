using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RoutineEscape.Application.DateTimeResolution;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Application.Records;
using RoutineEscape.Bot.Telegram;
using RoutineEscape.Bot.Telegram.Handlers;
using RoutineEscape.Bot.Telegram.Sources;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;
using RoutineEscape.Infrastructure.Persistence;
using Telegram.Bot.Types;

namespace RoutineEscape.IntegrationTests.Telegram;

public sealed class ListCommandTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-16T12:00:00Z");

    [Theory]
    [InlineData("/start")]
    [InlineData("/help")]
    [InlineData("/menu@RoutineEscape_bot")]
    [InlineData("/today")]
    [InlineData("/upcoming")]
    [InlineData("/overdue")]
    [InlineData("/done")]
    [InlineData("/tasks")]
    [InlineData("/events")]
    [InlineData("/reminders")]
    [InlineData("/notes")]
    [InlineData("/all")]
    [InlineData("/search own")]
    [InlineData("/settings")]
    [InlineData("/typo")]
    public async Task NavigationCommands_DoNotBecomeDraftsOrEditInput_AndReplyPrivately(string command)
    {
        await using var db = Context(); var gateway = new Gateway(); var state = new RecordInteractionState();
        var pending = new RecordInteraction(Guid.NewGuid(), Intent.Note, Guid.NewGuid(), "text", "v", Now.AddMinutes(15));
        state.Set(123, pending);
        await Dispatcher(db, gateway, state).DispatchAsync(Update(command, -999), default);
        var response = Assert.Single(gateway.Messages);
        Assert.Equal(123, response.Chat); Assert.True(response.Text.Length < 2500);
        Assert.Equal(pending, state.Get(123)); Assert.Empty(db.Drafts);
        Assert.Contains("остаётся открытым", response.Text);
    }

    [Theory]
    [InlineData("/list", 123)]
    [InlineData("/list@RoutineEscape_bot", -999)]
    public async Task List_RoutesToBoundedPrivateSummaryWithoutDraftAndPreservesEdit(string command, long chat)
    {
        await using var db = Context();
        var owner = new AppUser(Guid.NewGuid(), 123, "Owner", null, null, Now, "Asia/Qyzylorda");
        var other = new AppUser(Guid.NewGuid(), 456, "Other", null, null, Now);
        db.Users.AddRange(owner, other);
        for (var i = 0; i < 6; i++) db.Notes.Add(new Note(Guid.NewGuid(), owner.Id, $"Own {i}", Now.AddMinutes(i)));
        db.Notes.Add(new Note(Guid.NewGuid(), other.Id, "Other private note", Now));
        await db.SaveChangesAsync();
        var gateway = new Gateway(); var state = new RecordInteractionState();
        var pending = new RecordInteraction(Guid.NewGuid(), Intent.Note, db.Notes.First(item => item.UserId == owner.Id).Id, "text", "v", Now.AddMinutes(15));
        state.Set(123, pending);
        await Dispatcher(db, gateway, state).DispatchAsync(Update(command, chat), default);
        var message = Assert.Single(gateway.Messages);
        Assert.Equal(123, message.Chat); // Never publish the private summary in the originating group.
        Assert.Contains("Ближайшие 3 дня", message.Text);
        Assert.Contains("Редактирование остаётся открытым", message.Text);
        Assert.DoesNotContain("Other private note", message.Text);
        Assert.Equal(5, message.Buttons.SelectMany(row => row).Count(button => button.CallbackData.StartsWith("rec:")));
        Assert.Contains(message.Buttons.SelectMany(row => row), button => button.Text == "Показать всё" && button.CallbackData == "records:page:0");
        Assert.Empty(db.Drafts);
        Assert.Equal(pending, state.Get(123));
    }

    [Fact]
    public async Task List_EmptyUserGetsClearMessageAndShowAllButtonWithoutCreatingUserOrDraft()
    {
        await using var db = Context(); var gateway = new Gateway();
        await Dispatcher(db, gateway, new RecordInteractionState()).DispatchAsync(Update("/list"), default);
        var message = Assert.Single(gateway.Messages);
        Assert.Contains("Пока нет актуальных записей", message.Text);
        Assert.Contains(message.Buttons.SelectMany(row => row), button => button.CallbackData == "records:page:0");
        Assert.Contains(message.Buttons.SelectMany(row => row), button => button.CallbackData == "browse:menu");
        Assert.Empty(db.Drafts); Assert.Empty(db.Users);
    }

    [Fact]
    public async Task List_RecordsOutsideWindowAreNotReportedAsEmptyAccount()
    {
        await using var db = Context(); var user = new AppUser(Guid.NewGuid(), 123, "Owner", null, null, Now, "Asia/Qyzylorda");
        db.Users.Add(user); db.Tasks.Add(new TaskItem(Guid.NewGuid(), user.Id, "Later", Now, deadlineUtc: Now.AddDays(10))); await db.SaveChangesAsync();
        var gateway = new Gateway(); await Dispatcher(db, gateway, new RecordInteractionState()).DispatchAsync(Update("/list"), default);
        var message = Assert.Single(gateway.Messages);
        Assert.Contains("На ближайшие 3 дня записей нет", message.Text);
        Assert.Contains("Ещё записей в полном списке: 1", message.Text);
        Assert.DoesNotContain("Пока нет актуальных записей", message.Text);
    }

    private static RoutineEscapeDbContext Context() => new(new DbContextOptionsBuilder<RoutineEscapeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    [Fact]
    public async Task TimeZoneChange_DoesNotMoveExistingUtcDeadlines_AndRejectsUnknownZone()
    {
        await using var db = Context();
        var user = new AppUser(Guid.NewGuid(), 123, "Owner", null, null, Now, "UTC");
        db.Users.Add(user);
        var task = new TaskItem(Guid.NewGuid(), user.Id, "Saved", Now, deadlineUtc: Now.AddDays(1)); db.Tasks.Add(task);
        await db.SaveChangesAsync();
        var gateway = new Gateway();
        var handler = new TimeZoneCommandHandler(new EfRepository<AppUser>(db), db, gateway, new RecordInteractionState(), new Clock());
        await handler.HandleAsync(Update("/timezone Asia/Qyzylorda").Message!, default);
        Assert.Equal("Asia/Qyzylorda", user.TimeZoneId); Assert.Equal(Now.AddDays(1), task.DeadlineUtc);
        await handler.HandleAsync(Update("/timezone Not/AZone").Message!, default);
        Assert.Equal("Asia/Qyzylorda", user.TimeZoneId); Assert.Contains("Неизвестный", gateway.Messages.Last().Text);
    }

    private static Update Update(string text, long chat = 123) => new() { Id = 1, Message = new Message { Id = 42, Text = text, From = new User { Id = 123, FirstName = "Owner" }, Chat = new Chat { Id = chat } } };
    private static TelegramUpdateDispatcher Dispatcher(RoutineEscapeDbContext db, Gateway gateway, RecordInteractionState states)
    {
        var clock = new Clock();
        var users = new EfRepository<AppUser>(db); var tasks = new EfRepository<TaskItem>(db); var events = new EfRepository<CalendarEvent>(db);
        var reminders = new EfRepository<Reminder>(db); var notes = new EfRepository<Note>(db); var resolver = new RussianDateTimeResolver();
        var overview = new RecordOverviewService(users, tasks, events, reminders, notes);
        var browser = new RecordBrowserHandler(overview, gateway, states, new SearchSessions(), clock);
        var drafts = new DraftFlowService(users, new EfRepository<MessageSource>(db), new EfRepository<Draft>(db), tasks, events, reminders, notes, resolver, db);
        var management = new RecordManagementHandler(new RecordManagementService(users, tasks, events, reminders, notes, resolver, db), overview, states, gateway, clock, NullLogger<RecordManagementHandler>.Instance);
        return new TelegramUpdateDispatcher(new StartCommandHandler(gateway), new TextMessageHandler(gateway, new TelegramMessageSourceExtractor(),
            new MessageSourceDisplayFormatter(), drafts, new RuleBasedMessageInterpreter(), clock, NullLogger<TextMessageHandler>.Instance),
            new CallbackQueryHandler(gateway, drafts, clock, overview, management, recordBrowser: browser), NullLogger<TelegramUpdateDispatcher>.Instance,
            management, new ListCommandHandler(overview, gateway, states, clock), recordBrowser: browser);
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Gateway : ITelegramBotGateway
    {
        public List<(long Chat, string Text, IReadOnlyList<IReadOnlyList<BotButton>> Buttons)> Messages { get; } = [];
        public Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken, IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null) { Messages.Add((chatId, text, buttons ?? [])); return Task.CompletedTask; }
        public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
