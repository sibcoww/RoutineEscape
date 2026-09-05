using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Drafts;

public sealed record DraftCreationResult(
    Guid DraftId,
    DateTimeOffset ExpiresAtUtc,
    Intent SuggestedIntent = Intent.Unknown,
    decimal Confidence = 0m,
    string? SuggestedTitle = null,
    string? DateExpression = null,
    string? TimeExpression = null,
    string? Location = null);
