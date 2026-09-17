using RoutineEscape.Application.Abstractions.Persistence;
using RoutineEscape.Application.DateTimeResolution;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Domain.Common;
using RoutineEscape.Domain.Entities;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Records;

public sealed class RecordManagementService(IRepository<AppUser> users, IRepository<TaskItem> tasks,
    IRepository<CalendarEvent> events, IRepository<Reminder> reminders, IRepository<Note> notes,
    IDateTimeResolver resolver, IUnitOfWork unitOfWork) : IRecordManagementService
{
    public async Task<RecordDetails> GetAsync(long userId, Intent intent, Guid id, CancellationToken ct)
    {
        var (user, entity) = await OwnedAsync(userId, intent, id, ct);
        return Details(user, entity);
    }

    public async Task<RecordDetails> SetCompletedAsync(long userId, Guid id, bool completed, DateTimeOffset now, CancellationToken ct)
    {
        var (user, entity) = await OwnedAsync(userId, Intent.Task, id, ct);
        var task = (TaskItem)entity;
        if (completed && task.Status != TaskItemStatus.Completed) task.Complete(now);
        if (!completed && task.Status != TaskItemStatus.Pending) task.Reopen();
        await unitOfWork.SaveChangesAsync(ct);
        return Details(user, entity);
    }

    public async Task<RecordDetails> EditAsync(long userId, Intent intent, Guid id, string field, string input, DateTimeOffset now, CancellationToken ct)
    {
        var (user, entity) = await OwnedAsync(userId, intent, id, ct);
        var value = input.Trim();
        if (field == "text")
        {
            var limit = intent == Intent.Note ? 3500 : 500;
            if (value.Length == 0 || value.Length > limit)
                throw new RecordInputException($"Введите текст длиной от 1 до {limit} символов. Изменения пока не сохранены.");
            switch (entity)
            {
                case TaskItem task: task.Rename(value); break;
                case CalendarEvent calendar: calendar.Rename(value); break;
                case Reminder reminder: reminder.Rename(value); break;
                case Note note: note.Update(value, null, note.Tags.ToArray(), now); break;
            }
        }
        else if (field == "date" && intent != Intent.Note)
        {
            if (value.Length is 0 or > 200) throw new RecordInputException("Введите дату длиной от 1 до 200 символов.");
            DateTimeOffset? at = null;
            var explicitTime = false;
            if (!(intent == Intent.Task && value == "-"))
            {
                var parsed = await new RuleBasedMessageInterpreter().InterpretAsync(value, ct);
                explicitTime = parsed.TimeExpression is not null || parsed.DateExpression?.StartsWith("через ", StringComparison.Ordinal) == true;
                if (parsed.DateExpression is null || (intent != Intent.Task && parsed.TimeExpression is null
                    && !parsed.DateExpression.StartsWith("через ", StringComparison.Ordinal)))
                    throw new RecordInputException("Укажите дату и время: «завтра в 19:00» или «через 40 минут». Для задачи можно указать только дату.");
                try
                {
                    var resolution = resolver.Resolve(parsed.DateExpression, parsed.TimeExpression, now, user.TimeZoneId);
                    at = resolution.Value.ToUniversalTime();
                    if (resolution.IsAmbiguous || (intent != Intent.Task && at <= now)) throw new FormatException();
                }
                catch (Exception error) when (error is FormatException or ArgumentException or OverflowException or TimeZoneNotFoundException or InvalidTimeZoneException)
                {
                    throw new RecordInputException("Дата не распознана, неоднозначна или уже прошла. Уточните месяц, например: «23 сентября в 13:00». Исходная запись не изменена.");
                }
            }
            switch (entity)
            {
                case TaskItem task: task.ChangeDeadline(at, explicitTime); break;
                case CalendarEvent calendar: calendar.Reschedule(at!.Value); break;
                case Reminder reminder: reminder.Reschedule(at!.Value); break;
            }
        }
        else throw new RecordInputException("Это поле недоступно для редактирования.");
        await unitOfWork.SaveChangesAsync(ct);
        return Details(user, entity);
    }

    public async Task DeleteAsync(long userId, Intent intent, Guid id, CancellationToken ct)
    {
        var (_, entity) = await OwnedAsync(userId, intent, id, ct);
        switch (entity)
        {
            case TaskItem task: tasks.Remove(task); break;
            case CalendarEvent calendar: events.Remove(calendar); break;
            case Reminder reminder: reminders.Remove(reminder); break;
            case Note note: notes.Remove(note); break;
        }
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<(AppUser User, IEntity Entity)> OwnedAsync(long telegramUserId, Intent intent, Guid id, CancellationToken ct)
    {
        var user = (await users.ListAsync(item => item.TelegramUserId == telegramUserId, ct)).SingleOrDefault()
            ?? throw new KeyNotFoundException("Record unavailable.");
        IEntity? entity = intent switch
        {
            Intent.Task => await tasks.GetByIdAsync(id, ct), Intent.Event => await events.GetByIdAsync(id, ct),
            Intent.Reminder => await reminders.GetByIdAsync(id, ct), Intent.Note => await notes.GetByIdAsync(id, ct), _ => null,
        };
        var owner = entity switch { TaskItem item => item.UserId, CalendarEvent item => item.UserId, Reminder item => item.UserId, Note item => item.UserId, _ => Guid.Empty };
        if (entity is null || owner != user.Id) throw new KeyNotFoundException("Record unavailable.");
        return (user, entity);
    }

    private static RecordDetails Details(AppUser user, IEntity entity) => entity switch
    {
        TaskItem item => new(item.Id, Intent.Task, item.Title, item.DeadlineUtc, user.TimeZoneId, item.Status.ToString(), item.HasExplicitTime),
        CalendarEvent item => new(item.Id, Intent.Event, item.Title, item.StartUtc, user.TimeZoneId, "Active"),
        Reminder item => new(item.Id, Intent.Reminder, item.Title, item.TriggerAtUtc, user.TimeZoneId, item.Status.ToString()),
        Note item => new(item.Id, Intent.Note, item.Content, null, user.TimeZoneId, "Active"),
        _ => throw new ArgumentOutOfRangeException(nameof(entity)),
    };
}
