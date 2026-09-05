namespace RoutineEscape.Application.Drafts;

public sealed record DraftCreationResult(Guid DraftId, DateTimeOffset ExpiresAtUtc);
