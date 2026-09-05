using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RoutineEscape.Bot.Controllers;
using RoutineEscape.Bot.Telegram;
using Telegram.Bot.Types;

namespace RoutineEscape.IntegrationTests.Telegram;

public sealed class TelegramWebhookControllerTests
{
    [Fact]
    public async Task Post_RejectsInvalidWebhookSecret()
    {
        var dispatcher = new StubDispatcher();
        var controller = CreateController(dispatcher, "wrong-secret");

        var result = await controller.Post(new Update { Id = 1 }, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, dispatcher.CallCount);
    }

    [Fact]
    public async Task Post_DispatchesUpdateWithValidWebhookSecret()
    {
        var dispatcher = new StubDispatcher();
        var controller = CreateController(dispatcher, "expected-secret");

        var result = await controller.Post(new Update { Id = 1 }, CancellationToken.None);

        Assert.IsType<OkResult>(result);
        Assert.Equal(1, dispatcher.CallCount);
    }

    [Fact]
    public async Task Post_ReturnsServerErrorWhenDispatcherFails()
    {
        var controller = CreateController(new StubDispatcher(new InvalidOperationException("failure")),
            "expected-secret");

        var result = await controller.Post(new Update { Id = 1 }, CancellationToken.None);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    private static TelegramWebhookController CreateController(
        ITelegramUpdateDispatcher dispatcher,
        string suppliedSecret)
    {
        var controller = new TelegramWebhookController(
            dispatcher,
            Options.Create(new TelegramOptions { WebhookSecret = "expected-secret" }),
            NullLogger<TelegramWebhookController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.Request.Headers["X-Telegram-Bot-Api-Secret-Token"] = suppliedSecret;
        return controller;
    }

    private sealed class StubDispatcher(Exception? exception = null) : ITelegramUpdateDispatcher
    {
        public int CallCount { get; private set; }

        public Task DispatchAsync(Update update, CancellationToken cancellationToken)
        {
            CallCount++;
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }
}
