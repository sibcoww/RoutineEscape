using Microsoft.EntityFrameworkCore;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;
using RoutineEscape.Infrastructure.Persistence;

namespace RoutineEscape.IntegrationTests.Drafts;

public sealed class DraftFlowServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

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
        context);
}
