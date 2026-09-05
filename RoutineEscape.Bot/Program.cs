using Microsoft.Extensions.Options;
using RoutineEscape.Bot.Telegram;
using RoutineEscape.Bot.Telegram.Handlers;
using RoutineEscape.Bot.Telegram.Sources;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Infrastructure.Persistence;
using RoutineEscape.AI;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Application.DateTimeResolution;
using Telegram.Bot;
using Telegram.Bot.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.ConfigureTelegramBotMvc();
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));
builder.Services.AddHttpClient("telegram-bot");
builder.Services.AddSingleton<ITelegramBotClient>(provider =>
{
    var options = provider.GetRequiredService<IOptions<TelegramOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.BotToken))
    {
        throw new InvalidOperationException("Telegram:BotToken is not configured.");
    }

    var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("telegram-bot");
    return new TelegramBotClient(options.BotToken, httpClient);
});
builder.Services.AddSingleton<ITelegramBotGateway, TelegramBotGateway>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMessageSourceExtractor, TelegramMessageSourceExtractor>();
builder.Services.AddSingleton<MessageSourceDisplayFormatter>();
builder.Services.AddScoped<StartCommandHandler>();
builder.Services.AddScoped<TextMessageHandler>();
builder.Services.AddScoped<CallbackQueryHandler>();
builder.Services.AddScoped<ITelegramUpdateDispatcher, TelegramUpdateDispatcher>();
builder.Services.AddScoped<IDraftFlowService, DraftFlowService>();
builder.Services.AddSingleton<IDateTimeResolver, RussianDateTimeResolver>();
var aiOptions = builder.Configuration.GetSection("AI").Get<OpenAiCompatibleOptions>() ?? new();
builder.Services.AddSingleton(aiOptions);
builder.Services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>();
builder.Services.AddScoped<IMessageInterpreter, LlmMessageInterpreter>();
builder.Services.AddHostedService<TelegramLongPollingService>();

var connectionString = builder.Configuration.GetConnectionString("RoutineEscape");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("ROUTINEESCAPE_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=routineescape;Username=routineescape;Password=routineescape_dev";
}
builder.Services.AddRoutineEscapePersistence(connectionString);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program;
