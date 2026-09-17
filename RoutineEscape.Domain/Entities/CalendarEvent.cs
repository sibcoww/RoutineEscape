using RoutineEscape.Domain.Common;

namespace RoutineEscape.Domain.Entities;

public sealed class CalendarEvent : IEntity
{
    private CalendarEvent()
    {
    }

    public CalendarEvent(Guid id, Guid userId, string title, DateTimeOffset startUtc,
        DateTimeOffset createdAtUtc, DateTimeOffset? endUtc = null, string? description = null,
        string? location = null, Guid? sourceId = null, string? externalCalendarId = null)
    {
        Id = DomainGuard.Required(id, nameof(id));
        UserId = DomainGuard.Required(userId, nameof(userId));
        Title = DomainGuard.Required(title, nameof(title));
        StartUtc = DomainGuard.Utc(startUtc, nameof(startUtc));
        CreatedAt = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        EndUtc = endUtc is null ? null : DomainGuard.Utc(endUtc.Value, nameof(endUtc));
        if (EndUtc <= StartUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(endUtc), "Event end must be after its start.");
        }

        Description = NormalizeOptional(description);
        Location = NormalizeOptional(location);
        SourceId = sourceId;
        ExternalCalendarId = NormalizeOptional(externalCalendarId);
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public DateTimeOffset StartUtc { get; private set; }
    public DateTimeOffset? EndUtc { get; private set; }
    public string? Location { get; private set; }
    public Guid? SourceId { get; private set; }
    public string? ExternalCalendarId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public void Rename(string title) => Title = DomainGuard.Required(title, nameof(title));
    public void Reschedule(DateTimeOffset startUtc)
    {
        var start = DomainGuard.Utc(startUtc, nameof(startUtc));
        var end = EndUtc is null ? (DateTimeOffset?)null : start.Add(EndUtc.Value - StartUtc);
        StartUtc = start;
        EndUtc = end;
    }
}
