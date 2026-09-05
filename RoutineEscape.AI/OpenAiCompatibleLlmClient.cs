using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace RoutineEscape.AI;

public sealed class OpenAiCompatibleLlmClient(HttpClient httpClient, OpenAiCompatibleOptions options) : ILlmClient
{
    public async Task<string> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("AI endpoint must be an absolute URI.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("AI API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException("AI model is not configured.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        message.Content = JsonContent.Create(new
        {
            model = options.Model,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                new { role = "user", content = request.UserMessage },
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "message_interpretation",
                    strict = true,
                    schema = JsonDocument.Parse(request.JsonSchema).RootElement,
                },
            },
        });

        using var response = await httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LLM request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content");
            if (content.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(content.GetString()))
            {
                throw new InvalidLlmResponseException("LLM response contains no message content.");
            }

            return content.GetString()!;
        }
        catch (InvalidLlmResponseException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or
                                          InvalidOperationException or IndexOutOfRangeException)
        {
            throw new InvalidLlmResponseException("LLM transport response has an unexpected shape.", exception);
        }
    }
}
