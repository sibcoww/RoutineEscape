using Microsoft.Extensions.Options;
using RoutineEscape.Bot.Telegram;
using RoutineEscape.Bot.Telegram.Handlers;
using RoutineEscape.Bot.Telegram.Sources;
using RoutineEscape.Application.Drafts;
using RoutineEscape.Infrastructure.Persistence;
using RoutineEscape.Application.Interpretation;
using RoutineEscape.Application.DateTimeResolution;
using Telegram.Bot;
using Telegram.Bot.AspNetCore;
using RoutineEscape.Bot.Logging;
using RoutineEscape.Application.Records;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new DailyLogProvider(
    Path.Combine(builder.Environment.ContentRootPath, "logs"), new SecretRedactor(builder.Configuration)));

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
builder.Services.AddScoped<CallbackResponseContext>();
builder.Services.AddScoped<ITelegramBotGateway, TelegramBotGateway>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMessageSourceExtractor, TelegramMessageSourceExtractor>();
builder.Services.AddSingleton<MessageSourceDisplayFormatter>();
builder.Services.AddScoped<StartCommandHandler>();
builder.Services.AddScoped<ListCommandHandler>();
builder.Services.AddScoped<RecordBrowserHandler>();
builder.Services.AddScoped<IRecordBrowserService, RecordOverviewService>();
builder.Services.AddSingleton<SearchSessions>();
builder.Services.AddScoped<TimeZoneCommandHandler>();
builder.Services.AddScoped<NotificationCallbackHandler>();
builder.Services.AddScoped<NotificationProcessor>();
builder.Services.AddScoped<INotificationSender, TelegramNotificationSender>();
builder.Services.AddScoped<TextMessageHandler>();
builder.Services.AddScoped<CallbackQueryHandler>();
builder.Services.AddScoped<ITelegramUpdateDispatcher, TelegramUpdateDispatcher>();
builder.Services.AddScoped<IDraftFlowService, DraftFlowService>();
builder.Services.AddScoped<IRecordOverviewService, RecordOverviewService>();
builder.Services.AddScoped<IRecordManagementService, RecordManagementService>();
builder.Services.AddSingleton(new RecordInteractionState(Path.Combine(builder.Environment.ContentRootPath, "state", "record-interactions.json")));
builder.Services.AddScoped<RecordManagementHandler>();
builder.Services.AddSingleton<IDateTimeResolver, RussianDateTimeResolver>();
builder.Services.AddSingleton<IMessageInterpreter, RuleBasedMessageInterpreter>();
builder.Services.AddHostedService<TelegramLongPollingService>();
builder.Services.AddHostedService<NotificationScheduler>();

var connectionString = builder.Configuration.GetConnectionString("RoutineEscape");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = Environment.GetEnvironmentVariable("ROUTINEESCAPE_CONNECTION_STRING");
}
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Database connection string is not configured.");
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
