namespace RoutineEscape.AI;

public sealed class OpenAiCompatibleOptions
{
    public string Endpoint { get; init; } = "https://api.openai.com/v1/chat/completions";
    public string ApiKey { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
}
