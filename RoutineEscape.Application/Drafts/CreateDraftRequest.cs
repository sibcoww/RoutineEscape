using RoutineEscape.Domain.Entities;
using RoutineEscape.Application.Interpretation;

namespace RoutineEscape.Application.Drafts;

public sealed record CreateDraftRequest(
    long TelegramUserId,
    string FirstName,
    string? LastName,
    string? Username,
    long TelegramMessageId,
    string Text,
    MessageSource Source,
    DateTimeOffset CreatedAtUtc,
    MessageInterpretation? Interpretation = null);
