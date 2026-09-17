using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RoutineEscape.Application.Records;
using RoutineEscape.Bot.Telegram;
using RoutineEscape.Bot.Telegram.Handlers;
using RoutineEscape.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace RoutineEscape.IntegrationTests.Telegram;

public sealed class ResponsePresentationTests
{
    [Fact]
    public async Task CallbackHandler_ConnectsReplacementContextToListResponse()
    {
        var context = new CallbackResponseContext();
        var transport = new Transport(); using var http = new HttpClient(transport);
        var handler = new CallbackQueryHandler(Gateway(http, context), null!, TimeProvider.System,
            new EmptyRecordOverview(), responses: context);
        var callback = Callback(); callback.Data = "records:page:0";
        await handler.HandleAsync(callback, default);
        Assert.Equal(new[] { "answerCallbackQuery", "sendMessage", "deleteMessage" }, transport.Calls.Select(call => call.Method));
    }

    [Fact]
    public async Task Replacement_SendsHtmlBeforeDeletingClickedMessage_OnlyOnce()
    {
        var context = new CallbackResponseContext(); context.Begin(Callback());
        var transport = new Transport(); using var http = new HttpClient(transport);
        var gateway = Gateway(http, context);
        await gateway.SendHtmlMessageAsync(123, "<b>Название</b>\n<i>сегодня</i>", default);
        await gateway.SendTextMessageAsync(123, "Дополнение <без разметки>", default);
        Assert.Equal(new[] { "sendMessage", "deleteMessage", "sendMessage" }, transport.Calls.Select(call => call.Method));
        using var sent = JsonDocument.Parse(transport.Calls[0].Body);
        Assert.Equal("Html", sent.RootElement.GetProperty("parse_mode").GetString());
        using var deleted = JsonDocument.Parse(transport.Calls[1].Body);
        Assert.Equal(42, deleted.RootElement.GetProperty("message_id").GetInt32());
        Assert.Equal(123, deleted.RootElement.GetProperty("chat_id").GetInt64());
        using var plain = JsonDocument.Parse(transport.Calls[2].Body);
        Assert.False(plain.RootElement.TryGetProperty("parse_mode", out _));
    }

    [Fact]
    public async Task FailedSend_PreservesOldMessage_AndFailedDeleteDoesNotFailSuccessfulReply()
    {
        var context = new CallbackResponseContext(); context.Begin(Callback());
        var transport = new Transport { FailMethod = "sendMessage" }; using var http = new HttpClient(transport);
        var gateway = Gateway(http, context);
        await Assert.ThrowsAnyAsync<Exception>(() => gateway.SendTextMessageAsync(123, "Ответ", default));
        Assert.Single(transport.Calls); Assert.Equal("sendMessage", transport.Calls[0].Method);
        transport.FailMethod = "deleteMessage";
        await gateway.SendTextMessageAsync(123, "Ответ", default);
        Assert.Equal(new[] { "sendMessage", "sendMessage", "deleteMessage" }, transport.Calls.Select(call => call.Method));
        await gateway.SendTextMessageAsync(123, "Ещё ответ", default);
        Assert.Equal(1, transport.Calls.Count(call => call.Method == "deleteMessage"));
    }

    [Theory]
    [InlineData(false, 123, 123)]
    [InlineData(true, -999, 123)]
    [InlineData(true, 123, 456)]
    public async Task Replacement_DoesNotDeleteUserOrGroupMessagesOrOtherChat(bool bot, long sourceChat, long destination)
    {
        var context = new CallbackResponseContext(); context.Begin(Callback(bot, sourceChat));
        var transport = new Transport(); using var http = new HttpClient(transport);
        await Gateway(http, context).SendTextMessageAsync(destination, "Ответ", default);
        Assert.Equal("sendMessage", Assert.Single(transport.Calls).Method);
    }

    [Fact]
    public async Task CallbackOnlyAndBackgroundNotification_DoNotDeleteNavigation()
    {
        var context = new CallbackResponseContext(); context.Begin(Callback());
        var transport = new Transport(); using var http = new HttpClient(transport);
        await Gateway(http, context).AnswerCallbackQueryAsync("cb", "Кнопка устарела", default);
        await Gateway(http, new CallbackResponseContext()).SendTextMessageAsync(123, "Плановое уведомление", default);
        Assert.Equal(new[] { "answerCallbackQuery", "sendMessage" }, transport.Calls.Select(call => call.Method));
        Assert.Equal(42, context.TakePrevious(123));
    }

    [Fact]
    public void Overview_FormatsDateAndOverdue_AndEscapesUserMarkup()
    {
        var now = DateTimeOffset.Parse("2026-09-17T12:00:00Z");
        var record = new SavedRecord(Guid.NewGuid(), Intent.Task, "Купить <b>чай</b> & кофе", now.AddMinutes(-1), now, true, "Pending");
        var line = RecordOverviewFormatter.Line(record, "Asia/Qyzylorda", now);
        Assert.Contains("<b>Купить &lt;b&gt;чай&lt;/b&gt; &amp; кофе</b>", line);
        Assert.Contains("<i>сегодня 16:59</i>", line);
        Assert.Contains("⚠️ <b>просрочено</b>", line);
        Assert.DoesNotContain("<b>чай</b>", line);
        var complete = RecordOverviewFormatter.Line(record with { Status = "Completed" }, "Asia/Qyzylorda", now);
        Assert.DoesNotContain("просрочено", complete);
        Assert.Contains("выполнено", complete);
        var buttons = RecordOverviewFormatter.PageButtons(new("Asia/Qyzylorda", [record], 1, 0, 1, 0));
        Assert.Contains(buttons.SelectMany(row => row), button => button.Text.Contains("<b>чай</b> & кофе")); // Button labels are plain text.
    }

    private static CallbackQuery Callback(bool bot = true, long chat = 123) => new()
    {
        Id = "cb", From = new User { Id = 123, FirstName = "Owner" }, ChatInstance = "x",
        Message = new Message { Id = 42, From = new User { Id = 999, IsBot = bot, FirstName = "Bot" }, Chat = new Chat { Id = chat } },
    };
    private static TelegramBotGateway Gateway(HttpClient http, CallbackResponseContext context) =>
        new(new TelegramBotClient("123456789:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijk", http), NullLogger<TelegramBotGateway>.Instance, context);
    private sealed class Transport : HttpMessageHandler
    {
        public string? FailMethod { get; set; }
        public List<(string Method, string Body)> Calls { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var method = request.RequestUri!.Segments.Last();
            Calls.Add((method, await request.Content!.ReadAsStringAsync(ct)));
            if (method == FailMethod) return new(HttpStatusCode.BadRequest) { Content = new StringContent("{\"ok\":false,\"error_code\":400,\"description\":\"unavailable\"}") };
            return new(HttpStatusCode.OK) { Content = new StringContent(method == "sendMessage"
                ? "{\"ok\":true,\"result\":{\"message_id\":88,\"date\":1789531200,\"chat\":{\"id\":123,\"type\":\"private\"},\"text\":\"reply\"}}"
                : "{\"ok\":true,\"result\":true}") };
        }
    }
}
