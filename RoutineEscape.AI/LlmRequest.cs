namespace RoutineEscape.AI;

public sealed record LlmRequest(string SystemPrompt, string UserMessage, string JsonSchema);
