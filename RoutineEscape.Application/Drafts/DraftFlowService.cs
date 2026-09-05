using System.Text.Json;
using RoutineEscape.Application.Abstractions.Persistence;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Drafts;

public sealed class DraftFlowService(
    IRepository<AppUser> users,
    IRepository<MessageSource> sources,
    IRepository<Draft> drafts,
    IRepository<TaskItem> tasks,
    IRepository<CalendarEvent> events,
    IRepository<Reminder> reminders,
    IRepository<Note> notes,
    IUnitOfWork unitOfWork) : IDraftFlowService
{
    private static readonly TimeSpan DraftLifetime = TimeSpan.FromHours(1);

    public async Task<DraftCreationResult> CreateAsync(
        CreateDraftRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Text);
        var user = (await users.ListAsync(cancellationToken))
            .SingleOrDefault(candidate => candidate.TelegramUserId == request.TelegramUserId);
        if (user is null)
        {
            user = new AppUser(Guid.NewGuid(), request.TelegramUserId, request.FirstName,
                request.LastName, request.Username, request.CreatedAtUtc);
            await users.AddAsync(user, cancellationToken);
        }

        await sources.AddAsync(request.Source, cancellationToken);
        var expiresAt = request.CreatedAtUtc.Add(DraftLifetime);
        var payload = JsonSerializer.Serialize(new DraftPayload(request.Text, request.Source.Id));
        var draft = new Draft(Guid.NewGuid(), user.Id, request.TelegramMessageId, Intent.Unknown,
            0m, payload, expiresAt, request.CreatedAtUtc);
        await drafts.AddAsync(draft, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DraftCreationResult(draft.Id, expiresAt);
    }

    public async Task<DraftSelectionResult> SelectTypeAsync(Guid draftId, long telegramUserId,
        Intent intent, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        if (intent is Intent.Unknown)
        {
            throw new ArgumentException("A concrete intent is required.", nameof(intent));
        }

        var user = (await users.ListAsync(cancellationToken))
            .SingleOrDefault(candidate => candidate.TelegramUserId == telegramUserId)
            ?? throw new KeyNotFoundException("User was not found.");
        var draft = await drafts.GetByIdAsync(draftId, cancellationToken)
            ?? throw new KeyNotFoundException("Draft was not found.");
        if (draft.UserId != user.Id)
        {
            throw new UnauthorizedAccessException("Draft belongs to another user.");
        }

        if (draft.Expire(nowUtc))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Draft has expired.");
        }

        var payload = JsonSerializer.Deserialize<DraftPayload>(draft.PayloadJson)
            ?? throw new InvalidOperationException("Draft payload is invalid.");
        await AddEntityAsync(intent, user.Id, payload, nowUtc, cancellationToken);
        draft.Confirm(nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DraftSelectionResult(intent, payload.Text);
    }

    public async Task CancelAsync(Guid draftId, long telegramUserId, CancellationToken cancellationToken)
    {
        var user = (await users.ListAsync(cancellationToken))
            .SingleOrDefault(candidate => candidate.TelegramUserId == telegramUserId)
            ?? throw new KeyNotFoundException("User was not found.");
        var draft = await drafts.GetByIdAsync(draftId, cancellationToken)
            ?? throw new KeyNotFoundException("Draft was not found.");
        if (draft.UserId != user.Id)
        {
            throw new UnauthorizedAccessException("Draft belongs to another user.");
        }

        draft.Cancel();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task AddEntityAsync(Intent intent, Guid userId, DraftPayload payload,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        switch (intent)
        {
            case Intent.Task:
                await tasks.AddAsync(new TaskItem(Guid.NewGuid(), userId, payload.Text, nowUtc,
                    sourceId: payload.SourceId, originalText: payload.Text), cancellationToken);
                break;
            case Intent.Event:
                await events.AddAsync(new CalendarEvent(Guid.NewGuid(), userId, payload.Text, nowUtc,
                    nowUtc, sourceId: payload.SourceId), cancellationToken);
                break;
            case Intent.Reminder:
                await reminders.AddAsync(new Reminder(Guid.NewGuid(), userId, payload.Text, nowUtc,
                    nowUtc, sourceId: payload.SourceId), cancellationToken);
                break;
            case Intent.Note:
                await notes.AddAsync(new Note(Guid.NewGuid(), userId, payload.Text, nowUtc,
                    sourceId: payload.SourceId), cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent));
        }
    }

    private sealed record DraftPayload(string Text, Guid SourceId);
}
