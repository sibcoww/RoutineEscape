using RoutineEscape.Infrastructure.Persistence;

namespace RoutineEscape.Bot.Telegram;

public sealed class NotificationScheduler(IServiceScopeFactory scopes, TimeProvider clock, ILogger<NotificationScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Notification scheduler started: persistent schedules, 5-second checks, one follow-up");
        var ready = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                for (var i = 0; i < 20; i++)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    if (!await scope.ServiceProvider.GetRequiredService<NotificationProcessor>().ProcessOneAsync(clock.GetUtcNow(), stoppingToken)) break;
                }
                if (!ready) { logger.LogInformation("Notification scheduler ready: database checked"); ready = true; }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Notification scheduler iteration failed; will retry"); }
            await Task.Delay(TimeSpan.FromSeconds(5), clock, stoppingToken);
        }
    }
}
