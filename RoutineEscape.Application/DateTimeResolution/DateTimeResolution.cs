namespace RoutineEscape.Application.DateTimeResolution;

public sealed record DateTimeResolution(DateTimeOffset Value, bool IsAmbiguous = false);
