using RoutineEscape.Domain.Common;

namespace RoutineEscape.Domain.Entities;

public sealed class Note : IEntity
{
    private readonly List<string> _tags = [];

    private Note()
    {
    }

    public Note(Guid id, Guid userId, string content, DateTimeOffset createdAtUtc,
        string? title = null, Guid? sourceId = null, IEnumerable<string>? tags = null)
    {
        Id = DomainGuard.Required(id, nameof(id));
        UserId = DomainGuard.Required(userId, nameof(userId));
        Content = DomainGuard.Required(content, nameof(content));
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        SourceId = sourceId;
        CreatedAt = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAt = CreatedAt;

        if (tags is not null)
        {
            _tags.AddRange(NormalizeTags(tags));
        }
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string? Title { get; private set; }
    public string Content { get; private set; } = null!;
    public Guid? SourceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    public void Update(string content, string? title, IEnumerable<string> tags, DateTimeOffset updatedAtUtc)
    {
        var timestamp = DomainGuard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        if (timestamp < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedAtUtc));
        }

        Content = DomainGuard.Required(content, nameof(content));
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        _tags.Clear();
        _tags.AddRange(NormalizeTags(tags));
        UpdatedAt = timestamp;
    }

    private static IEnumerable<string> NormalizeTags(IEnumerable<string> tags) => tags
        .Select(tag => DomainGuard.Required(tag, nameof(tags)).TrimStart('#'))
        .Where(tag => tag.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase);
}
