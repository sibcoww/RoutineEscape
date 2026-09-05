using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RoutineEscape.Bot.Telegram;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Controllers;

[ApiController]
[Route("api/telegram/webhook")]
public sealed class TelegramWebhookController(
    ITelegramUpdateDispatcher dispatcher,
    IOptions<TelegramOptions> options,
    ILogger<TelegramWebhookController> logger) : ControllerBase
{
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    [HttpPost]
    public async Task<IActionResult> Post(Update update, CancellationToken cancellationToken)
    {
        if (!IsValidSecret(Request.Headers[SecretHeader].ToString(), options.Value.WebhookSecret))
        {
            logger.LogWarning("Rejected Telegram webhook request with an invalid secret");
            return Unauthorized();
        }

        try
        {
            await dispatcher.DispatchAsync(update, cancellationToken);
            return Ok();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to process Telegram update {UpdateId}", update.Id);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static bool IsValidSecret(string supplied, string configured)
    {
        if (string.IsNullOrWhiteSpace(supplied) || string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var configuredBytes = Encoding.UTF8.GetBytes(configured);
        return suppliedBytes.Length == configuredBytes.Length
            && CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }
}
