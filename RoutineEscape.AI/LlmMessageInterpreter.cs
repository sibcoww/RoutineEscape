using System.Text.Json;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.AI;

public sealed class LlmMessageInterpreter(ILlmClient client) : IMessageInterpreter
{
    internal const string Schema = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["intent", "confidence", "title", "description", "date_expression", "time_expression", "location", "person"],
          "properties": {
            "intent": { "type": "string", "enum": ["task", "event", "reminder", "note", "unknown"] },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
            "title": { "type": "string", "minLength": 1, "maxLength": 500 },
            "description": { "type": ["string", "null"], "maxLength": 4000 },
            "date_expression": { "type": ["string", "null"], "maxLength": 200 },
            "time_expression": { "type": ["string", "null"], "maxLength": 200 },
            "location": { "type": ["string", "null"], "maxLength": 500 },
            "person": { "type": ["string", "null"], "maxLength": 500 }
          }
        }
        """;

    private const string Prompt = """
        Interpret the user's message for a personal organizer. Return only an object matching the supplied JSON schema.
        Preserve unresolved date and time phrases verbatim in date_expression and time_expression; do not calculate dates.
        Normalize title into a short actionable title. Do not invent missing facts; use null for absent optional values.
        Use task for an action, event for a calendar occurrence, reminder for an explicit reminder, note for information,
        and unknown only when the message cannot be classified. Confidence must reflect actual classification certainty.
        """;

    public async Task<MessageInterpretation> InterpretAsync(
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message is required.", nameof(message));
        }

        var json = await client.CompleteAsync(new LlmRequest(Prompt, message.Trim(), Schema), cancellationToken);
        return Parse(json);
    }

    internal static MessageInterpretation Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidLlmResponseException("LLM returned an empty response.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("The response must be a JSON object.");
            }

            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                "intent", "confidence", "title", "description", "date_expression",
                "time_expression", "location", "person",
            };
            foreach (var property in root.EnumerateObject())
            {
                if (!allowed.Remove(property.Name))
                {
                    throw Invalid($"Unexpected or duplicate property '{property.Name}'.");
                }
            }

            if (allowed.Count != 0)
            {
                throw Invalid($"Missing required property '{allowed.First()}'.");
            }

            var intentText = RequiredString(root, "intent", 20).ToLowerInvariant();
            var intent = intentText switch
            {
                "task" => Intent.Task,
                "event" => Intent.Event,
                "reminder" => Intent.Reminder,
                "note" => Intent.Note,
                "unknown" => Intent.Unknown,
                _ => throw Invalid($"Unsupported intent '{intentText}'."),
            };

            var confidenceElement = root.GetProperty("confidence");
            if (confidenceElement.ValueKind != JsonValueKind.Number ||
                !confidenceElement.TryGetDecimal(out var confidence) || confidence is < 0 or > 1)
            {
                throw Invalid("Confidence must be a number from 0 to 1.");
            }

            return new MessageInterpretation(
                intent,
                confidence,
                RequiredString(root, "title", 500),
                OptionalString(root, "description", 4000),
                OptionalString(root, "date_expression", 200),
                OptionalString(root, "time_expression", 200),
                OptionalString(root, "location", 500),
                OptionalString(root, "person", 500));
        }
        catch (InvalidLlmResponseException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new InvalidLlmResponseException("LLM response is not valid JSON.", exception);
        }
    }

    private static string RequiredString(JsonElement root, string name, int maximumLength)
    {
        var element = root.GetProperty(name);
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw Invalid($"Property '{name}' must be a non-empty string.");
        }

        var value = element.GetString()!.Trim();
        return value.Length <= maximumLength
            ? value
            : throw Invalid($"Property '{name}' exceeds {maximumLength} characters.");
    }

    private static string? OptionalString(JsonElement root, string name, int maximumLength)
    {
        var element = root.GetProperty(name);
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            throw Invalid($"Property '{name}' must be a string or null.");
        }

        var value = element.GetString()!.Trim();
        if (value.Length > maximumLength)
        {
            throw Invalid($"Property '{name}' exceeds {maximumLength} characters.");
        }

        return value.Length == 0 ? null : value;
    }

    private static InvalidLlmResponseException Invalid(string message) => new(message);
}
