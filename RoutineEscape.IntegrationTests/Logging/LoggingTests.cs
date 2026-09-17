using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RoutineEscape.Bot.Logging;
using RoutineEscape.Bot.Telegram;
using RoutineEscape.Bot.Telegram.Handlers;
using RoutineEscape.Bot.Telegram.Sources;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace RoutineEscape.IntegrationTests.Logging;

public sealed class LoggingTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "routineescape-log-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void BothSinks_RedactSecretsAndExceptions_AndSuppressHttpDiagnostics()
    {
        const string token = "123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Telegram:BotToken"] = token, ["AI:ApiKey"] = "private-api-key",
            ["ConnectionStrings:RoutineEscape"] = "Host=localhost;Password=db-secret",
        }).Build();
        var console = new StringWriter();
        using var provider = new DailyLogProvider(directory, new SecretRedactor(configuration), console: console);
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        var logger = factory.CreateLogger("test");
        using (logger.BeginScope("key={Key}", "private-api-key"))
            logger.LogError(new InvalidOperationException("unlabelled-secret-from-exception"),
                "token={Token} db={Password} password=unknown-password https://api.telegram.org/bot{Token}/sendMessage",
                token, "db-secret", token);
        factory.CreateLogger("System.Net.Http.HttpClient.telegram").LogCritical("unlabelled-http-secret");
        logger.LogInformation("Input {Text}", "{\"password\":\"json-secret\"} пароль russian-secret https://user:url-secret@example.com");
        var file = File.ReadAllText(Assert.Single(Directory.GetFiles(directory)));
        Assert.Equal(console.ToString(), file);
        foreach (var secret in new[] { token, "private-api-key", "db-secret", "unknown-password", "unlabelled-secret-from-exception", "unlabelled-http-secret", "api.telegram.org", "json-secret", "russian-secret", "url-secret" })
            Assert.DoesNotContain(secret, file);
        Assert.Contains("InvalidOperationException", file);
        Assert.Contains("REDACTED", file);
        using var parsed = JsonDocument.Parse(file.Split(Environment.NewLine)[0]);
        Assert.Equal("Error", parsed.RootElement.GetProperty("level").GetString());
    }

    [Fact]
    public void Files_RotateAtUtcMidnight_AndRetainOnlySevenCalendarDays()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "routineescape-2026-09-09.jsonl"), "expired");
        File.WriteAllText(Path.Combine(directory, "routineescape-2026-09-10.jsonl"), "oldest-kept");
        File.WriteAllText(Path.Combine(directory, "unrelated.txt"), "keep");
        var clock = new MutableClock(DateTimeOffset.Parse("2026-09-16T23:59:00Z"));
        using var provider = new DailyLogProvider(directory, new SecretRedactor(new ConfigurationBuilder().Build()), clock, new StringWriter());
        var logger = provider.CreateLogger("test");
        logger.LogInformation("first-day");
        Assert.False(File.Exists(Path.Combine(directory, "routineescape-2026-09-09.jsonl")));
        Assert.True(File.Exists(Path.Combine(directory, "routineescape-2026-09-10.jsonl")));
        clock.Now = clock.Now.AddMinutes(2);
        logger.LogInformation("second-day");
        Assert.False(File.Exists(Path.Combine(directory, "routineescape-2026-09-10.jsonl")));
        Assert.Contains("first-day", File.ReadAllText(Path.Combine(directory, "routineescape-2026-09-16.jsonl")));
        Assert.Contains("second-day", File.ReadAllText(Path.Combine(directory, "routineescape-2026-09-17.jsonl")));
        Assert.True(File.Exists(Path.Combine(directory, "unrelated.txt")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_LogsInputTypePreparedReplyAndRealTransportOutcome(bool fail)
    {
        var console = new StringWriter();
        using var provider = new DailyLogProvider(directory, new SecretRedactor(new ConfigurationBuilder().Build()), console: console);
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        using var http = new HttpClient(new TelegramTransport(fail));
        var client = new TelegramBotClient("123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk", http);
        var gateway = new TelegramBotGateway(client, factory.CreateLogger<TelegramBotGateway>());
        var drafts = new StubDrafts();
        var dispatcher = new TelegramUpdateDispatcher(new StartCommandHandler(gateway),
            new TextMessageHandler(gateway, new TelegramMessageSourceExtractor(), new MessageSourceDisplayFormatter(),
                drafts, new RuleBasedMessageInterpreter(), TimeProvider.System, factory.CreateLogger<TextMessageHandler>()),
            new CallbackQueryHandler(gateway, drafts, TimeProvider.System, new EmptyRecordOverview()), factory.CreateLogger<TelegramUpdateDispatcher>());
        var update = new Update { Id = 42, Message = new Message { Id = 7, Text = "созвон завтра в 19:00",
            From = new User { Id = 123, FirstName = "Test" },
            Chat = new Chat { Id = 123, Type = global::Telegram.Bot.Types.Enums.ChatType.Private } } };
        if (fail) await Assert.ThrowsAnyAsync<Exception>(() => dispatcher.DispatchAsync(update, CancellationToken.None));
        else await dispatcher.DispatchAsync(update, CancellationToken.None);
        var output = console.ToString();
        Assert.Contains("text=созвон завтра в 19:00", output);
        Assert.Contains("Recognized type=Event", output);
        Assert.Contains("Response prepared", output);
        Assert.Contains("Похоже на событие", output);
        Assert.Contains(fail ? "Telegram response failed" : "Telegram accepted response", output);
        if (fail) Assert.DoesNotContain("Telegram accepted response", output);
        if (fail) Assert.Contains("errorCode=400", output);
        Assert.DoesNotContain("sensitive-transport-details", output);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var scopes = lines.Select(line => JsonDocument.Parse(line).RootElement.GetProperty("scopes")[0].GetString()).ToArray();
        Assert.Single(scopes.Distinct());
        Assert.Contains("UpdateId=42 ProcessingId=", scopes[0]);
        Assert.Equal(output, File.ReadAllText(Assert.Single(Directory.GetFiles(directory))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackReply_LogsTransportOutcome(bool fail)
    {
        var console = new StringWriter();
        using var provider = new DailyLogProvider(directory, new SecretRedactor(new ConfigurationBuilder().Build()), console: console);
        using var factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        using var http = new HttpClient(new TelegramTransport(fail, callback: true));
        var gateway = new TelegramBotGateway(new TelegramBotClient("123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk", http), factory.CreateLogger<TelegramBotGateway>());
        if (fail) await Assert.ThrowsAnyAsync<Exception>(() => gateway.AnswerCallbackQueryAsync("callback", "Сохранено", CancellationToken.None));
        else await gateway.AnswerCallbackQueryAsync("callback", "Сохранено", CancellationToken.None);
        Assert.Contains("text=Сохранено", console.ToString());
        Assert.Contains(fail ? "Telegram response failed" : "Telegram accepted response", console.ToString());
    }

    public void Dispose()
    {
        var target = Path.GetFullPath(directory);
        if (Path.GetDirectoryName(target) != Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))
            || !Path.GetFileName(target).StartsWith("routineescape-log-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected test directory.");
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class TelegramTransport(bool fail, bool callback = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (fail) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                { Content = new StringContent("{\"ok\":false,\"error_code\":400,\"description\":\"sensitive-transport-details\"}") });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(callback
                ? "{\"ok\":true,\"result\":true}"
                : "{\"ok\":true,\"result\":{\"message_id\":88,\"date\":1789531200,\"chat\":{\"id\":123,\"type\":\"private\"},\"text\":\"reply\"}}") });
        }
    }

    private sealed class StubDrafts : IDraftFlowService
    {
        public Task<DraftCreationResult> CreateAsync(CreateDraftRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new DraftCreationResult(Guid.NewGuid(), request.CreatedAtUtc.AddHours(1), request.Interpretation!.Intent,
                request.Interpretation.Confidence, request.Interpretation.Title, request.Interpretation.DateExpression,
                request.Interpretation.TimeExpression, null));
        public Task<DraftSelectionResult> SelectTypeAsync(Guid draftId, long telegramUserId, Intent intent, DateTimeOffset nowUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelAsync(Guid draftId, long telegramUserId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
