using RoutineEscape.Domain.Entities;

namespace RoutineEscape.Application.Drafts;

public sealed record CreateDraftRequest(
    long TelegramUserId,
    string FirstName,
    string? LastName,
    string? Username,
    long TelegramMessageId,
    string Text,
    MessageSource Source,
    DateTimeOffset CreatedAtUtc);
