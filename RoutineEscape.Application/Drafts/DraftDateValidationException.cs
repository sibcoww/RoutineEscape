namespace RoutineEscape.Application.Drafts;

public sealed class DraftDateValidationException(string message) : InvalidOperationException(message);
