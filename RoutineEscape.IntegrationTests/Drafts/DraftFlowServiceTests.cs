using Microsoft.EntityFrameworkCore;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Application.DateTimeResolution;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;
using RoutineEscape.Infrastructure.Persistence;

namespace RoutineEscape.IntegrationTests.Drafts;

public sealed class DraftFlowServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("23го числа")]
    [InlineData("23-го")]
    [InlineData("23 го")]
    [InlineData("23 числа")]
    public async Task ManualEvent_DayOnlyPreservesTextAndSchedulesCorrectInstant(string day)
    {
        await using var context = CreateContext();
        var now = DateTimeOffset.Parse("2035-09-16T10:00:00Z");
        context.Users.Add(new AppUser(Guid.NewGuid(), 123, "Owner", null, null, now, timeZoneId: "Asia/Qyzylorda"));
        await context.SaveChangesAsync();
        var text = $"{day} в 13:00 собес типа";
        var parsed = await new RuleBasedMessageInterpreter().InterpretAsync(text, default);
        Assert.Equal(Intent.Unknown, parsed.Intent);
        Assert.Equal(day, parsed.DateExpression);
        Assert.Equal(text, parsed.Title);
        var service = CreateService(context);
        var draft = await service.CreateAsync(new CreateDraftRequest(123, "Owner", null, null, 42,
            text, MessageSource.Direct(Guid.NewGuid(), 123, now), now, parsed), default);
        Assert.Empty(context.NotificationSchedules);
        var confirmation = await service.SelectTypeAsync(draft.DraftId, 123, Intent.Event, now.AddMinutes(1), default);
        var saved = await context.Events.SingleAsync();
        var expected = DateTimeOffset.Parse("2035-09-23T08:00:00Z");
        Assert.Equal(text, saved.Title);
        Assert.Equal(text, confirmation.Title);
        Assert.Equal(expected, confirmation.AtUtc);
        Assert.Equal(expected, saved.StartUtc);
        Assert.Equal(expected, (await context.NotificationSchedules.SingleAsync()).DueAt);
    }

    [Fact]
    public async Task CreateAndSelectNote_PersistsDraftSourceUserAndEntity()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var source = MessageSource.Direct(Guid.NewGuid(), 123, Now);

        var created = await service.CreateAsync(new CreateDraftRequest(
            123, "Ivan", null, "ivan", 42, "Room 305", source, Now), CancellationToken.None);
        var result = await service.SelectTypeAsync(created.DraftId, 123, Intent.Note,
            Now.AddMinutes(1), CancellationToken.None);

        Assert.Equal(Intent.Note, result.Intent);
        Assert.Equal("Room 305", (await context.Notes.SingleAsync()).Content);
        Assert.Equal(DraftStatus.Confirmed, (await context.Drafts.SingleAsync()).Status);
        Assert.Equal(source.Id, (await context.Notes.SingleAsync()).SourceId);
        Assert.Equal(123, (await context.Users.SingleAsync()).TelegramUserId);
    }

    [Fact]
    public async Task SelectType_RejectsAnotherUser()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var source = MessageSource.Direct(Guid.NewGuid(), 123, Now);
        var created = await service.CreateAsync(new CreateDraftRequest(
            123, "Owner", null, null, 42, "Private", source, Now), CancellationToken.None);
        context.Users.Add(new AppUser(Guid.NewGuid(), 456, "Other", null, null, Now));
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SelectTypeAsync(
            created.DraftId, 456, Intent.Note, Now.AddMinutes(1), CancellationToken.None));
        Assert.Empty(context.Notes);
    }

    [Fact]
    public async Task CreateAndConfirmAiDraft_PersistsNormalizedFieldsOnlyAfterConfirmation()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var source = MessageSource.Direct(Guid.NewGuid(), 123, Now);
        var interpretation = new MessageInterpretation(Intent.Task, 0.91m,
            "Отправить документы", "Диме", "до среды", "вечером", null, "Дима");

        var created = await service.CreateAsync(new CreateDraftRequest(
            123, "Ivan", null, null, 42, "Скинь Диме документы", source, Now, interpretation),
            CancellationToken.None);

        Assert.Empty(context.Tasks);
        Assert.Equal(Intent.Task, created.SuggestedIntent);
        Assert.Equal(0.91m, created.Confidence);
        await service.SelectTypeAsync(created.DraftId, 123, Intent.Task,
            Now.AddMinutes(1), CancellationToken.None);

        var task = await context.Tasks.SingleAsync();
        Assert.Equal("Отправить документы", task.Title);
        Assert.Equal("Диме", task.Description);
        Assert.Equal("Скинь Диме документы", task.OriginalText);
    }

    [Theory]
    [InlineData("созвон завтра в 19:00")]
    [InlineData("созвон завтра в девятнадцать ноль ноль")]
    public async Task Rules_ConfirmEventWithUserTimeZone(string text)
    {
        await using var context = CreateContext();
        context.Users.Add(new AppUser(Guid.NewGuid(), 123, "Owner", null, null, Now,
            timeZoneId: "Asia/Qyzylorda"));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var interpretation = await new RuleBasedMessageInterpreter().InterpretAsync(text, CancellationToken.None);
        var draft = await service.CreateAsync(new CreateDraftRequest(123, "Owner", null, null, 42,
            text, MessageSource.Direct(Guid.NewGuid(), 123, Now), Now, interpretation), CancellationToken.None);
        Assert.Empty(context.Events);
        await service.SelectTypeAsync(draft.DraftId, 123, interpretation.Intent, Now.AddMinutes(1), CancellationToken.None);
        var saved = await context.Events.SingleAsync();
        Assert.Equal(DateTimeOffset.Parse("2026-09-06T14:00:00Z"), saved.StartUtc);
    }

    [Theory]
    [InlineData("созвон завтра")]
    [InlineData("созвон в 19:00")]
    [InlineData("созвон завтра в 25:00")]
    [InlineData("созвон завтра или послезавтра в 19:00")]
    [InlineData("созвон 31 сентября в 19:00")]
    [InlineData("созвон в следующую пятницу в 19:00")]
    public async Task Rules_RejectMissingOrInvalidDateWithoutSaving(string text)
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var interpretation = await new RuleBasedMessageInterpreter().InterpretAsync(text, CancellationToken.None);
        var draft = await service.CreateAsync(new CreateDraftRequest(123, "Owner", null, null, 42,
            text, MessageSource.Direct(Guid.NewGuid(), 123, Now), Now, interpretation), CancellationToken.None);
        await Assert.ThrowsAsync<DraftDateValidationException>(() => service.SelectTypeAsync(
            draft.DraftId, 123, Intent.Event, Now.AddMinutes(1), CancellationToken.None));
        Assert.Empty(context.Events);
        Assert.Equal(DraftStatus.Pending, (await context.Drafts.SingleAsync()).Status);
    }

    [Fact]
    public async Task Rules_RelativeReminderUsesMessageTimeInsteadOfConfirmationTime()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        const string text = "напомни через 40 минут выключить духовку";
        var interpretation = await new RuleBasedMessageInterpreter().InterpretAsync(text, CancellationToken.None);
        var draft = await service.CreateAsync(new CreateDraftRequest(123, "Owner", null, null, 42,
            text, MessageSource.Direct(Guid.NewGuid(), 123, Now), Now, interpretation), CancellationToken.None);
        await service.SelectTypeAsync(draft.DraftId, 123, Intent.Reminder, Now.AddMinutes(5), CancellationToken.None);
        Assert.Equal(Now.AddMinutes(40), (await context.Reminders.SingleAsync()).TriggerAtUtc);
    }

    private static RoutineEscapeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RoutineEscapeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new RoutineEscapeDbContext(options);
    }

    private static DraftFlowService CreateService(RoutineEscapeDbContext context) => new(
        new EfRepository<AppUser>(context),
        new EfRepository<MessageSource>(context),
        new EfRepository<Draft>(context),
        new EfRepository<TaskItem>(context),
        new EfRepository<CalendarEvent>(context),
        new EfRepository<Reminder>(context),
        new EfRepository<Note>(context),
        new RussianDateTimeResolver(),
        context);
}
