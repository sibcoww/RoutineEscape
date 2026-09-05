namespace RoutineEscape.Application.DateTimeResolution;

public interface IDateTimeResolver
{
    DateTimeResolution Resolve(string? dateExpression, string? timeExpression,
        DateTimeOffset nowUtc, string timeZoneId);
}
