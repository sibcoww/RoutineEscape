using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Drafts;

public sealed record DraftSelectionResult(Intent Intent, string Title, Guid? EntityId = null, DateTimeOffset? AtUtc = null, bool HasExplicitTime = false);
