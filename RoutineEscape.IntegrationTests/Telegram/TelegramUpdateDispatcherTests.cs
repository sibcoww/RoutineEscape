using Microsoft.Extensions.Logging.Abstractions;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Bot.Telegram;
using RoutineEscape.Bot.Telegram.Handlers;
using RoutineEscape.Bot.Telegram.Sources;
using RoutineEscape.Domain.Enums;
using Telegram.Bot.Types;

namespace RoutineEscape.IntegrationTests.Telegram;

public sealed class TelegramUpdateDispatcherTests
{
    [Theory]
    [InlineData("/start")]
    [InlineData("/start@RoutineEscapeBot payload")]
    public async Task DispatchAsync_RoutesStartCommand(string text)
    {
        var gateway = new RecordingGateway();
        var dispatcher = CreateDispatcher(gateway);

        await dispatcher.DispatchAsync(MessageUpdate(text), CancellationToken.None);

        var message = Assert.Single(gateway.Messages);
        Assert.Equal(123, message.ChatId);
        Assert.Contains("RoutineEscape", message.Text);
    }

    [Fact]
    public async Task DispatchAsync_RoutesTextMessageToDraftFlow()
    {
        var gateway = new RecordingGateway();
        var dispatcher = CreateDispatcher(gateway);

        await dispatcher.DispatchAsync(MessageUpdate("Buy milk"), CancellationToken.None);

        var message = Assert.Single(gateway.Messages);
        Assert.Equal("Как сохранить сообщение?", message.Text);
        Assert.NotNull(message.Buttons);
        Assert.Equal(5, message.Buttons.SelectMany(row => row).Count());
    }

    [Fact]
    public async Task DispatchAsync_AnswersValidDraftCallback()
    {
        var gateway = new RecordingGateway();
        var dispatcher = CreateDispatcher(gateway);
        var update = new Update
        {
            Id = 3,
            CallbackQuery = new CallbackQuery
            {
                Id = "callback-1",
                From = new User { Id = 10, FirstName = "Ivan" },
                ChatInstance = "instance",
                Data = $"draft:type:Task:{Guid.NewGuid():N}",
            },
        };

        await dispatcher.DispatchAsync(update, CancellationToken.None);

        Assert.Equal("callback-1", Assert.Single(gateway.CallbackAnswers).Id);
    }

    private static TelegramUpdateDispatcher CreateDispatcher(RecordingGateway gateway) => new(
        new StartCommandHandler(gateway),
        new TextMessageHandler(gateway, new TelegramMessageSourceExtractor(),
            new MessageSourceDisplayFormatter(), new StubDraftFlowService(), TimeProvider.System),
        new CallbackQueryHandler(gateway, new StubDraftFlowService(), TimeProvider.System),
        NullLogger<TelegramUpdateDispatcher>.Instance);

    private static Update MessageUpdate(string text) => new()
    {
        Id = 1,
        Message = new Message
        {
            Id = 2,
            Date = DateTime.UtcNow,
            Chat = new Chat { Id = 123, Type = global::Telegram.Bot.Types.Enums.ChatType.Private },
            From = new User { Id = 123, FirstName = "Owner" },
            Text = text,
        },
    };

    private sealed class RecordingGateway : ITelegramBotGateway
    {
        public List<(long ChatId, string Text, IReadOnlyList<IReadOnlyList<BotButton>>? Buttons)> Messages { get; } = [];
        public List<(string Id, string? Text)> CallbackAnswers { get; } = [];

        public Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken,
            IReadOnlyList<IReadOnlyList<BotButton>>? buttons = null)
        {
            Messages.Add((chatId, text, buttons));
            return Task.CompletedTask;
        }

        public Task AnswerCallbackQueryAsync(string callbackQueryId, string? text,
            CancellationToken cancellationToken)
        {
            CallbackAnswers.Add((callbackQueryId, text));
            return Task.CompletedTask;
        }
    }

    private sealed class StubDraftFlowService : IDraftFlowService
    {
        public Task<DraftCreationResult> CreateAsync(CreateDraftRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new DraftCreationResult(Guid.NewGuid(), request.CreatedAtUtc.AddHours(1)));

        public Task<DraftSelectionResult> SelectTypeAsync(Guid draftId, long telegramUserId,
            Intent intent, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult(new DraftSelectionResult(intent, "Test"));

        public Task CancelAsync(Guid draftId, long telegramUserId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
