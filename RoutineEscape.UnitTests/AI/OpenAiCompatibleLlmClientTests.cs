using System.Net;
using System.Text;
using RoutineEscape.AI;

namespace RoutineEscape.UnitTests.AI;

public sealed class OpenAiCompatibleLlmClientTests
{
    [Fact]
    public async Task CompleteAsync_SendsSchemaAndReturnsMessageContent()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"choices\":[{\"message\":{\"content\":\"{\\\"intent\\\":\\\"note\\\"}\"}}]}",
                Encoding.UTF8, "application/json"),
        });
        var client = new OpenAiCompatibleLlmClient(new HttpClient(handler), new OpenAiCompatibleOptions
        {
            Endpoint = "https://llm.example/v1/chat/completions",
            ApiKey = "secret",
            Model = "test-model",
        });

        var result = await client.CompleteAsync(
            new LlmRequest("system", "user", "{\"type\":\"object\"}"), CancellationToken.None);

        Assert.Equal("{\"intent\":\"note\"}", result);
        Assert.NotNull(handler.RequestBody);
        Assert.Contains("\"json_schema\"", handler.RequestBody);
        Assert.Contains("\"strict\":true", handler.RequestBody);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
    }

    [Fact]
    public async Task CompleteAsync_RejectsUnexpectedTransportResponse()
    {
        var handler = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        });
        var client = new OpenAiCompatibleLlmClient(new HttpClient(handler), ValidOptions());

        await Assert.ThrowsAsync<InvalidLlmResponseException>(() => client.CompleteAsync(
            new LlmRequest("system", "user", "{\"type\":\"object\"}"), CancellationToken.None));
    }

    private static OpenAiCompatibleOptions ValidOptions() => new()
    {
        Endpoint = "https://llm.example/v1/chat/completions",
        ApiKey = "secret",
        Model = "test-model",
    };

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public string? AuthorizationScheme { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            return response;
        }
    }
}
