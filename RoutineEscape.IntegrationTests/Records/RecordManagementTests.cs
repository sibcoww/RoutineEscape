using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RoutineEscape.Application.DateTimeResolution;
using RoutineEscape.Application.Records;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Bot.Telegram;
using RoutineEscape.Bot.Telegram.Handlers;
using RoutineEscape.Bot.Telegram.Sources;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;
using RoutineEscape.Infrastructure.Persistence;
using Telegram.Bot.Types;

namespace RoutineEscape.IntegrationTests.Records;

public sealed class RecordManagementTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-16T12:00:00Z");

    [Fact]
    public async Task EditDayOnly_PreservesTitleAndReschedules_RejectsInvalidDates()
    {
        await using var db = Context();
        var id = await SeedAsync(db, Intent.Event);
        var service = Service(db);
        var now = DateTimeOffset.Parse("2035-09-16T10:00:00Z");
        var result = await service.EditAsync(123, Intent.Event, id, "date", "23-го числа в 13:00", now, default);
        Assert.Equal("Original", result.Text);
        Assert.Equal(DateTimeOffset.Parse("2035-09-23T08:00:00Z"), result.AtUtc);
        Assert.Equal(result.AtUtc, (await db.NotificationSchedules.SingleAsync()).DueAt);
        foreach (var input in new[] { "15го в 13:00", "31 числа в 13:00" })
        {
            await Assert.ThrowsAsync<RecordInputException>(() => service.EditAsync(123, Intent.Event, id, "date", input, now, default));
            Assert.Equal(result, await service.GetAsync(123, Intent.Event, id, default));
        }
    }

    [Theory]
    [InlineData(Intent.Task)]
    [InlineData(Intent.Event)]
    [InlineData(Intent.Reminder)]
    [InlineData(Intent.Note)]
    public async Task EditAndDeleteEveryType_ValidatesOwnershipAndKeepsInvalidInputUnchanged(Intent intent)
    {
        await using var db = Context(); var id = await SeedAsync(db, intent);
        var service = Service(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(456, intent, id, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.EditAsync(456, intent, id, "text", "foreign", Now, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(456, intent, id, default));
        var edited = await service.EditAsync(123, intent, id, "text", "Новое название или содержимое", Now, default);
        Assert.Equal("Новое название или содержимое", edited.Text);
        if (intent == Intent.Note) Assert.Equal(new[] { "keep" }, (await db.Notes.SingleAsync()).Tags);
        await Assert.ThrowsAsync<RecordInputException>(() => service.EditAsync(123, intent, id, "text", "   ", Now, default));
        Assert.Equal(edited, await service.GetAsync(123, intent, id, default));
        if (intent != Intent.Note)
        {
            await Assert.ThrowsAsync<RecordInputException>(() => service.EditAsync(123, intent, id, "date", "завтра в 25:00", Now, default));
            Assert.Equal(edited, await service.GetAsync(123, intent, id, default));
            var dated = await service.EditAsync(123, intent, id, "date", "завтра в 19:00", Now, default);
            Assert.Equal(DateTimeOffset.Parse("2026-09-17T14:00:00Z"), dated.AtUtc);
        }
        await service.DeleteAsync(123, intent, id, default);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(123, intent, id, default));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(123, intent, id, default));
    }

    [Fact]
    public async Task TaskCompletion_IsIdempotentRestorableAndReflectedInOverview()
    {
        await using var db = Context(); var id = await SeedAsync(db, Intent.Task); var service = Service(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SetCompletedAsync(456, id, true, Now, default));
        await service.SetCompletedAsync(123, id, true, Now, default);
        await service.SetCompletedAsync(123, id, true, Now.AddMinutes(1), default);
        Assert.Equal(Now, (await db.Tasks.SingleAsync()).CompletedAt);
        Assert.Empty((await Overview(db).PageAsync(123, 0, Now, default)).Items);
        Assert.Equal(id, Assert.Single((await Overview(db).BrowseAsync(123, RecordView.Completed, null, 0, Now, default)).Items).Id);
        await service.SetCompletedAsync(123, id, false, Now, default);
        await service.SetCompletedAsync(123, id, false, Now, default);
        Assert.Null((await db.Tasks.SingleAsync()).CompletedAt);
        Assert.Empty((await Overview(db).BrowseAsync(123, RecordView.Completed, null, 0, Now, default)).Items);
        Assert.Single((await Overview(db).PageAsync(123, 0, Now, default)).Items);
        Assert.Null((await service.EditAsync(123, Intent.Task, id, "date", "-", Now, default)).AtUtc);
    }

    [Fact]
    public async Task EditSession_InvalidInputCanRetry_CancelDoesNotMutate_AndDoesNotCreateDraft()
    {
        await using var db = Context(); var id = await SeedAsync(db, Intent.Event);
        var states = new RecordInteractionState(); var gateway = new Gateway(); var clock = new Clock();
        var handler = Handler(db, states, gateway, clock);
        await handler.HandleCallbackAsync(await ActionAsync(handler, gateway, Intent.Event, id, "date"), default);
        var session = states.Get(123)!;
        Assert.True(await handler.HandleTextAsync(Message("непонятно", 1), default));
        Assert.Equal(session, states.Get(123));
        Assert.Equal(Now.AddDays(1), (await Service(db).GetAsync(123, Intent.Event, id, default)).AtUtc);
        Assert.Contains("Укажите дату", gateway.Messages.Last().Text);
        Assert.True(await handler.HandleTextAsync(Message("завтра в 19:00", 2), default));
        Assert.Null(states.Get(123));
        Assert.Equal(DateTimeOffset.Parse("2026-09-17T14:00:00Z"), (await Service(db).GetAsync(123, Intent.Event, id, default)).AtUtc);
        Assert.True(await handler.HandleTextAsync(Message("завтра в 19:00", 2), default)); // duplicate input consumed
        Assert.Empty(db.Drafts);
        await handler.HandleCallbackAsync(await ActionAsync(handler, gateway, Intent.Event, id, "text"), default);
        await handler.HandleTextAsync(Message("/cancel", 3), default);
        Assert.Equal("Original", (await Service(db).GetAsync(123, Intent.Event, id, default)).Text);
        Assert.Null(states.Get(123));
    }

    [Fact]
    public async Task DeleteRequiresConfirmation_CancelAndForeignAndRepeatedButtonsAreSafe()
    {
        await using var db = Context(); var id = await SeedAsync(db, Intent.Note);
        var states = new RecordInteractionState(); var gateway = new Gateway(); var handler = Handler(db, states, gateway, new Clock());
        await handler.HandleCallbackAsync(await ActionAsync(handler, gateway, Intent.Note, id, "del"), default);
        var session = states.Get(123)!;
        Assert.NotNull(await Service(db).GetAsync(123, Intent.Note, id, default));
        await handler.HandleCallbackAsync(Callback($"edit:delete:{session.Token:N}", 456), default);
        Assert.NotNull(await Service(db).GetAsync(123, Intent.Note, id, default));
        await handler.HandleCallbackAsync(Callback($"edit:cancel:{session.Token:N}"), default);
        Assert.NotNull(await Service(db).GetAsync(123, Intent.Note, id, default));
        await handler.HandleCallbackAsync(await ActionAsync(handler, gateway, Intent.Note, id, "del"), default);
        session = states.Get(123)!;
        var confirmation = Callback($"edit:delete:{session.Token:N}");
        await handler.HandleCallbackAsync(confirmation, default);
        await handler.HandleCallbackAsync(confirmation, default);
        Assert.Empty(db.Notes);
        Assert.Contains("устарела", gateway.Answers.Last());
    }

    [Fact]
    public async Task StaleButtonsCannotUndoNewerChanges_ExpiredTextDoesNotCreateAnything()
    {
        await using var db = Context(); var id = await SeedAsync(db, Intent.Task);
        var states = new RecordInteractionState(); var gateway = new Gateway(); var clock = new Clock(); var handler = Handler(db, states, gateway, clock);
        var oldComplete = await ActionAsync(handler, gateway, Intent.Task, id, "done");
        await handler.HandleCallbackAsync(oldComplete, default);
        await handler.HandleCallbackAsync(await ActionAsync(handler, gateway, Intent.Task, id, "undo"), default);
        // Even returning to the same status cannot reactivate an old card's action token.
        await handler.HandleCallbackAsync(oldComplete, default);
        Assert.Equal("Pending", (await Service(db).GetAsync(123, Intent.Task, id, default)).Status);
        await handler.HandleCallbackAsync(await ActionAsync(handler, gateway, Intent.Task, id, "text"), default);
        clock.Now = Now.AddMinutes(16);
        Assert.True(await handler.HandleTextAsync(Message("Should not save", 8), default));
        Assert.Equal("Original", (await Service(db).GetAsync(123, Intent.Task, id, default)).Text);
        Assert.Empty(db.Drafts);
        Assert.Contains("истекло", gateway.Messages.Last().Text);
    }

    [Fact]
    public async Task OtherUsersCannotOpenOrEditRecords_AndDeletedRecordDuringEditIsHandled()
    {
        await using var db = Context(); var id = await SeedAsync(db, Intent.Task);
        var states = new RecordInteractionState(); var gateway = new Gateway(); var handler = Handler(db, states, gateway, new Clock());
        await handler.HandleCallbackAsync(Callback($"rec:Task:{id:N}:open", 456), default);
        Assert.Empty(gateway.Messages);
        Assert.Contains("недоступна", gateway.Answers.Last());
        await handler.HandleCallbackAsync(await ActionAsync(handler, gateway, Intent.Task, id, "text"), default);
        Assert.False(await handler.HandleTextAsync(Message("Other input", 1, 456), default));
        await Service(db).DeleteAsync(123, Intent.Task, id, default);
        Assert.True(await handler.HandleTextAsync(Message("New title", 2), default));
        Assert.Contains("удалена", gateway.Messages.Last().Text);
        Assert.Null(states.Get(123));
    }

    [Fact]
    public void InteractionState_SurvivesRestartWithoutStoringMessageText()
    {
        var path = Path.Combine(Path.GetTempPath(), "routineescape-state-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var state = new RecordInteractionState(path);
            var session = new RecordInteraction(Guid.NewGuid(), Intent.Task, Guid.NewGuid(), "text", "version", Now.AddMinutes(15));
            state.Set(123, session); state.MarkHandled(123, 9);
            var restored = new RecordInteractionState(path);
            Assert.Equal(session, restored.Get(123)); Assert.True(restored.WasHandled(123, 9));
            restored.Clear(123); Assert.Null(new RecordInteractionState(path).Get(123));
        }
        finally { File.Delete(path); File.Delete(path + ".tmp"); }
    }

    [Fact]
    public async Task Dispatcher_RoutesEditInputWithoutCreatingDraft_ThenResumesNormalCreation()
    {
        await using var db = Context(); var id = await SeedAsync(db, Intent.Task);
        var states = new RecordInteractionState(); var gateway = new Gateway(); var clock = new Clock();
        var management = Handler(db, states, gateway, clock);
        var drafts = new DraftFlowService(new EfRepository<AppUser>(db), new EfRepository<MessageSource>(db),
            new EfRepository<Draft>(db), new EfRepository<TaskItem>(db), new EfRepository<CalendarEvent>(db),
            new EfRepository<Reminder>(db), new EfRepository<Note>(db), new RussianDateTimeResolver(), db);
        var dispatcher = new TelegramUpdateDispatcher(new StartCommandHandler(gateway),
            new TextMessageHandler(gateway, new TelegramMessageSourceExtractor(), new MessageSourceDisplayFormatter(),
                drafts, new RuleBasedMessageInterpreter(), clock, NullLogger<TextMessageHandler>.Instance),
            new CallbackQueryHandler(gateway, drafts, clock, Overview(db), management),
            NullLogger<TelegramUpdateDispatcher>.Instance, management);
        var callback = await ActionAsync(management, gateway, Intent.Task, id, "text");
        await dispatcher.DispatchAsync(new Update { Id = 1, CallbackQuery = callback }, default);
        await dispatcher.DispatchAsync(new Update { Id = 2, Message = Message("купить хлеб", 100) }, default);
        Assert.Empty(db.Drafts);
        Assert.Equal("купить хлеб", (await Service(db).GetAsync(123, Intent.Task, id, default)).Text);
        await dispatcher.DispatchAsync(new Update { Id = 3, Message = Message("купить молоко", 101) }, default);
        Assert.Single(db.Drafts);
        Assert.Single(db.Tasks); // the normal creation path still requires confirmation.
    }

    private static RoutineEscapeDbContext Context() => new(new DbContextOptionsBuilder<RoutineEscapeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static async Task<Guid> SeedAsync(RoutineEscapeDbContext db, Intent intent)
    {
        var owner = new AppUser(Guid.NewGuid(), 123, "Owner", null, null, Now.AddDays(-1), "Asia/Qyzylorda");
        db.Users.AddRange(owner, new AppUser(Guid.NewGuid(), 456, "Other", null, null, Now.AddDays(-1)));
        var id = Guid.NewGuid();
        switch (intent)
        {
            case Intent.Task: db.Tasks.Add(new TaskItem(id, owner.Id, "Original", Now.AddHours(-1))); break;
            case Intent.Event: db.Events.Add(new CalendarEvent(id, owner.Id, "Original", Now.AddDays(1), Now)); break;
            case Intent.Reminder: db.Reminders.Add(new Reminder(id, owner.Id, "Original", Now.AddDays(1), Now)); break;
            case Intent.Note: db.Notes.Add(new Note(id, owner.Id, "Original", Now, tags: ["keep"])); break;
        }
        await db.SaveChangesAsync(); return id;
    }
    private static RecordManagementService Service(RoutineEscapeDbContext db) => new(new EfRepository<AppUser>(db), new EfRepository<TaskItem>(db), new EfRepository<CalendarEvent>(db), new EfRepository<Reminder>(db), new EfRepository<Note>(db), new RussianDateTimeResolver(), db);
    private static RecordOverviewService Overview(RoutineEscapeDbContext db) => new(new EfRepository<AppUser>(db), new EfRepository<TaskItem>(db), new EfRepository<CalendarEvent>(db), new EfRepository<Reminder>(db), new EfRepository<Note>(db));
    private static RecordManagementHandler Handler(RoutineEscapeDbContext db, RecordInteractionState states, Gateway gateway, TimeProvider clock) => new(Service(db), Overview(db), states, gateway, clock, NullLogger<RecordManagementHandler>.Instance);
    private static async Task<CallbackQuery> ActionAsync(RecordManagementHandler handler, Gateway gateway, Intent intent, Guid id, string action)
    {
        await handler.HandleCallbackAsync(Callback($"rec:{intent}:{id:N}:open"), default);
        return Callback(gateway.Messages.Last().Buttons!.SelectMany(row => row).Single(button => button.CallbackData.Split(':').ElementAtOrDefault(3) == action).CallbackData);
    }
    private static CallbackQuery Callback(string data, long user = 123) => new() { Id = Guid.NewGuid().ToString(), From = new User { Id = user, FirstName = "Test" }, ChatInstance = "test", Data = data };
    private static Message Message(string text, int id, long user = 123) => new() { Id = id, Text = text, From = new User { Id = user, FirstName = "Test" }, Chat = new Chat { Id = user } };
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } = RecordManagementTests.Now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Gateway : ITelegramBotGateway
    {
        public List<(string Text, IReadOnlyList<IReadOnlyList<BotButton>>? Buttons)> Messages { get; } = [];
        public List<string?> Answers { get; } = [];
        public Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken, IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null) { Messages.Add((text, buttons)); return Task.CompletedTask; }
        public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text, CancellationToken cancellationToken) { Answers.Add(text); return Task.CompletedTask; }
    }
}
