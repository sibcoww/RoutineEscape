using System.Text.Json;
using RoutineEscape.Domain.Common;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Domain.Entities;

public sealed class Draft : IEntity
{
    private Draft()
    {
    }

    public Draft(Guid id, Guid userId, long telegramMessageId, Intent intent, decimal confidence,
        string payloadJson, DateTimeOffset expiresAtUtc, DateTimeOffset createdAtUtc)
    {
        Id = DomainGuard.Required(id, nameof(id));
        UserId = DomainGuard.Required(userId, nameof(userId));
        if (telegramMessageId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(telegramMessageId));
        }

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");
        }

        TelegramMessageId = telegramMessageId;
        Intent = intent;
        Confidence = confidence;
        PayloadJson = ValidateJson(payloadJson);
        CreatedAt = DomainGuard.Utc(createdAtUtc, nameof(createdAtUtc));
        ExpiresAt = DomainGuard.Utc(expiresAtUtc, nameof(expiresAtUtc));
        if (ExpiresAt <= CreatedAt)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Draft expiration must be after creation.");
        }

        Status = DraftStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public long TelegramMessageId { get; private set; }
    public Intent Intent { get; private set; }
    public decimal Confidence { get; private set; }
    public string PayloadJson { get; private set; } = null!;
    public DraftStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Confirm(DateTimeOffset nowUtc)
    {
        EnsurePending();
        if (DomainGuard.Utc(nowUtc, nameof(nowUtc)) >= ExpiresAt)
        {
            Status = DraftStatus.Expired;
            throw new InvalidOperationException("An expired draft cannot be confirmed.");
        }

        Status = DraftStatus.Confirmed;
    }

    public void Cancel()
    {
        EnsurePending();
        Status = DraftStatus.Cancelled;
    }

    public bool Expire(DateTimeOffset nowUtc)
    {
        EnsurePending();
        if (DomainGuard.Utc(nowUtc, nameof(nowUtc)) < ExpiresAt)
        {
            return false;
        }

        Status = DraftStatus.Expired;
        return true;
    }

    private void EnsurePending()
    {
        if (Status != DraftStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending draft can change state.");
        }
    }

    private static string ValidateJson(string payloadJson)
    {
        var value = DomainGuard.Required(payloadJson, nameof(payloadJson));
        try
        {
            using var _ = JsonDocument.Parse(value);
            return value;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Payload must contain valid JSON.", nameof(payloadJson), exception);
        }
    }
}
