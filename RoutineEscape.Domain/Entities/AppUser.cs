using RoutineEscape.Domain.Common;

namespace RoutineEscape.Domain.Entities;

public sealed class AppUser : IEntity
{
    private AppUser()
    {
    }

    public AppUser(
        Guid id,
        long telegramUserId,
        string firstName,
        string? lastName,
        string? username,
        DateTimeOffset createdAtUtc,
        string timeZoneId = "Asia/Almaty",
        string language = "ru")
    {
        Id = DomainGuard.Required(id, nameof(id));
        if (telegramUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telegramUserId));
        }

        TelegramUserId = telegramUserId;
        FirstName = DomainGuard.Required(firstName, nameof(firstName));
        LastName = NormalizeOptional(lastName);
        Username = NormalizeOptional(username)?.TrimStart('@');
        TimeZoneId = DomainGuard.Required(timeZoneId, nameof(timeZoneId));
        Language = DomainGuard.Required(language, nameof(language));
        CreatedAt = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public long TelegramUserId { get; private set; }
    public string? Username { get; private set; }
    public string FirstName { get; private set; } = null!;
    public string? LastName { get; private set; }
    public string TimeZoneId { get; private set; } = null!;
    public string Language { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void ChangeTimeZone(string timeZoneId, DateTimeOffset updatedAtUtc)
    {
        var timestamp = DomainGuard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        EnsureNotBeforeCreation(timestamp, nameof(updatedAtUtc));
        TimeZoneId = DomainGuard.Required(timeZoneId, nameof(timeZoneId));
        UpdatedAt = timestamp;
    }

    public void ChangeLanguage(string language, DateTimeOffset updatedAtUtc)
    {
        var timestamp = DomainGuard.Utc(updatedAtUtc, nameof(updatedAtUtc));
        EnsureNotBeforeCreation(timestamp, nameof(updatedAtUtc));
        Language = DomainGuard.Required(language, nameof(language));
        UpdatedAt = timestamp;
    }

    private void EnsureNotBeforeCreation(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp < CreatedAt)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Update cannot predate creation.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
