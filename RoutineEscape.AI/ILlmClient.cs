namespace RoutineEscape.AI;

public interface ILlmClient
{
    Task<string> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
}
