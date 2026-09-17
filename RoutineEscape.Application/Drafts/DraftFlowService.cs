using System.Text.Json;
using RoutineEscape.Application.Abstractions.Persistence;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;
using RoutineEscape.Application.DateTimeResolution;

namespace RoutineEscape.Application.Drafts;

public sealed class DraftFlowService(
    IRepository<AppUser> users,
    IRepository<MessageSource> sources,
    IRepository<Draft> drafts,
    IRepository<TaskItem> tasks,
    IRepository<CalendarEvent> events,
    IRepository<Reminder> reminders,
    IRepository<Note> notes,
    IDateTimeResolver dateTimeResolver,
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
        var interpretation = request.Interpretation;
        var intent = interpretation?.Intent ?? Intent.Unknown;
        var confidence = interpretation?.Confidence ?? 0m;
        var payload = JsonSerializer.Serialize(new DraftPayload(request.Text, request.Source.Id,
            interpretation?.Title ?? request.Text, interpretation?.Description,
            interpretation?.DateExpression, interpretation?.TimeExpression,
            interpretation?.Location, interpretation?.Person));
        var draft = new Draft(Guid.NewGuid(), user.Id, request.TelegramMessageId, intent,
            confidence, payload, expiresAt, request.CreatedAtUtc);
        await drafts.AddAsync(draft, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DraftCreationResult(draft.Id, expiresAt, intent, confidence,
            interpretation?.Title, interpretation?.DateExpression, interpretation?.TimeExpression,
            interpretation?.Location);
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
        var (entityId, atUtc) = await AddEntityAsync(intent, user, payload, draft.CreatedAt, nowUtc, cancellationToken);
        draft.Confirm(nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new DraftSelectionResult(intent, payload.Title ?? payload.Text, entityId, atUtc,
            payload.TimeExpression is not null || payload.DateExpression?.StartsWith("через ", StringComparison.OrdinalIgnoreCase) == true);
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

    private async Task<(Guid Id, DateTimeOffset? AtUtc)> AddEntityAsync(Intent intent, AppUser user, DraftPayload payload,
        DateTimeOffset referenceUtc, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        DateTimeOffset? resolvedAt = null;
        if (intent != Intent.Note)
        {
            var needsTime = intent is Intent.Event or Intent.Reminder;
            var relative = payload.DateExpression?.StartsWith("через ", StringComparison.OrdinalIgnoreCase) == true;
            if (needsTime && (payload.DateExpression is null || (!relative && payload.TimeExpression is null)))
                throw new DraftDateValidationException("Нужны дата и время. Отправьте сообщение заново, например: «созвон завтра в 19:00».");
            try
            {
                if (payload.DateExpression is not null || payload.TimeExpression is not null)
                {
                    var resolution = dateTimeResolver.Resolve(payload.DateExpression, payload.TimeExpression,
                        referenceUtc, user.TimeZoneId);
                    if (resolution.IsAmbiguous) throw new FormatException("Ambiguous local time.");
                    resolvedAt = resolution.Value.ToUniversalTime();
                    if (needsTime && resolvedAt <= nowUtc) throw new FormatException("Time has already passed.");
                }
            }
            catch (Exception exception) when (exception is FormatException or ArgumentOutOfRangeException
                or OverflowException or TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                throw new DraftDateValidationException("Уточните дату и время, например: «23 сентября в 13:00». Число без месяца относится к текущему месяцу и должно быть в будущем.");
            }
        }
        var entityId = Guid.NewGuid();
        switch (intent)
        {
            case Intent.Task:
                await tasks.AddAsync(new TaskItem(entityId, user.Id, payload.Title ?? payload.Text, nowUtc,
                    description: payload.Description, deadlineUtc: resolvedAt, sourceId: payload.SourceId,
                    originalText: payload.Text, hasExplicitTime: payload.TimeExpression is not null
                        || payload.DateExpression?.StartsWith("через ", StringComparison.OrdinalIgnoreCase) == true), cancellationToken);
                break;
            case Intent.Event:
                await events.AddAsync(new CalendarEvent(entityId, user.Id, payload.Title ?? payload.Text,
                    resolvedAt!.Value, nowUtc, description: payload.Description, location: payload.Location,
                    sourceId: payload.SourceId), cancellationToken);
                break;
            case Intent.Reminder:
                await reminders.AddAsync(new Reminder(entityId, user.Id, payload.Title ?? payload.Text,
                    resolvedAt!.Value, nowUtc, description: payload.Description,
                    sourceId: payload.SourceId), cancellationToken);
                break;
            case Intent.Note:
                await notes.AddAsync(new Note(entityId, user.Id, payload.Text, nowUtc,
                    title: payload.Title, sourceId: payload.SourceId), cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent));
        }
        return (entityId, resolvedAt);
    }

    private sealed record DraftPayload(string Text, Guid SourceId, string? Title, string? Description,
        string? DateExpression, string? TimeExpression, string? Location, string? Person);
}
