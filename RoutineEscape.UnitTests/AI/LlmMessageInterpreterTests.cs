using RoutineEscape.AI;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.UnitTests.AI;

public sealed class LlmMessageInterpreterTests
{
    [Fact]
    public async Task InterpretAsync_MapsValidStructuredResponse()
    {
        var client = new StubLlmClient("""
            {
              "intent": "task",
              "confidence": 0.93,
              "title": "Отправить Диме документы",
              "description": null,
              "date_expression": "до среды",
              "time_expression": "вечером",
              "location": null,
              "person": "Дима"
            }
            """);
        var interpreter = new LlmMessageInterpreter(client);

        var result = await interpreter.InterpretAsync(
            "Скинь Диме документы до среды вечером", CancellationToken.None);

        Assert.Equal(Intent.Task, result.Intent);
        Assert.Equal(0.93m, result.Confidence);
        Assert.Equal("Отправить Диме документы", result.Title);
        Assert.Equal("до среды", result.DateExpression);
        Assert.Equal("вечером", result.TimeExpression);
        Assert.Equal("Дима", result.Person);
        Assert.Equal("Скинь Диме документы до среды вечером", client.LastRequest!.UserMessage);
        Assert.Contains("additionalProperties", client.LastRequest.JsonSchema);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"intent\":\"task\"}")]
    [InlineData("{\"intent\":\"task\",\"confidence\":2,\"title\":\"x\",\"description\":null,\"date_expression\":null,\"time_expression\":null,\"location\":null,\"person\":null}")]
    [InlineData("{\"intent\":\"task\",\"confidence\":0.5,\"title\":\"x\",\"description\":null,\"date_expression\":null,\"time_expression\":null,\"location\":null,\"person\":null,\"extra\":true}")]
    public async Task InterpretAsync_RejectsInvalidStructuredResponse(string response)
    {
        var interpreter = new LlmMessageInterpreter(new StubLlmClient(response));

        await Assert.ThrowsAsync<InvalidLlmResponseException>(() =>
            interpreter.InterpretAsync("test", CancellationToken.None));
    }

    [Fact]
    public async Task InterpretAsync_RejectsBlankMessage()
    {
        var interpreter = new LlmMessageInterpreter(new StubLlmClient("{}"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            interpreter.InterpretAsync("  ", CancellationToken.None));
    }

    private sealed class StubLlmClient(string response) : ILlmClient
    {
        public LlmRequest? LastRequest { get; private set; }

        public Task<string> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }
}
