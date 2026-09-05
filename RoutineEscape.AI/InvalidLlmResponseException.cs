namespace RoutineEscape.AI;

public sealed class InvalidLlmResponseException : Exception
{
    public InvalidLlmResponseException(string message) : base(message)
    {
    }

    public InvalidLlmResponseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
