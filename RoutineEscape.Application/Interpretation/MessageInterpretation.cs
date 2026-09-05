using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Interpretation;

public sealed record MessageInterpretation(
    Intent Intent,
    decimal Confidence,
    string Title,
    string? Description,
    string? DateExpression,
    string? TimeExpression,
    string? Location,
    string? Person);
