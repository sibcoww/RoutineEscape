namespace RoutineEscape.Application.Interpretation;

public interface IMessageInterpreter
{
    Task<MessageInterpretation> InterpretAsync(string message, CancellationToken cancellationToken);
}
