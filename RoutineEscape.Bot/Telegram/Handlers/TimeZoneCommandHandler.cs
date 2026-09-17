using RoutineEscape.Application.Abstractions.Persistence;
using RoutineEscape.Domain.Entities;
using Telegram.Bot.Types;

namespace RoutineEscape.Bot.Telegram.Handlers;

public sealed class TimeZoneCommandHandler(IRepository<AppUser> users, IUnitOfWork unitOfWork,
    ITelegramBotGateway gateway, RecordInteractionState interactions, TimeProvider clock)
{
    public async Task HandleAsync(Message message, CancellationToken ct)
    {
        if (message.From is null) return;
        var sender = message.From;
        var argument = message.Text!.Split(' ', 2, StringSplitOptions.TrimEntries).ElementAtOrDefault(1);
        var existing = (await users.ListAsync(item => item.TelegramUserId == sender.Id, ct)).SingleOrDefault();
        if (string.IsNullOrWhiteSpace(argument))
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(existing?.TimeZoneId ?? "Asia/Almaty");
            await gateway.SendTextMessageAsync(sender.Id, $"Часовой пояс: {zone.Id}. Сейчас {TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone):dd.MM HH:mm zzz}.\nИзменить: /timezone Asia/Qyzylorda (UTC+05:00) или /timezone Europe/Moscow. Уже сохранённые моменты UTC не сдвигаются; новая зона влияет на ввод и отображение.", ct);
            return;
        }
        if (interactions.Get(sender.Id) is not null)
        {
            await gateway.SendTextMessageAsync(sender.Id, "Сначала завершите редактирование или отправьте /cancel, затем задайте часовой пояс.", ct); return;
        }
        TimeZoneInfo selected;
        try { selected = TimeZoneInfo.FindSystemTimeZoneById(argument); }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            await gateway.SendTextMessageAsync(sender.Id, "Неизвестный часовой пояс. Например: /timezone Asia/Qyzylorda или /timezone Europe/Moscow.", ct); return;
        }
        var user = existing is null ? new AppUser(Guid.NewGuid(), sender.Id, sender.FirstName, sender.LastName, sender.Username, clock.GetUtcNow())
            : (await users.GetByIdAsync(existing.Id, ct))!;
        if (existing is null) await users.AddAsync(user, ct);
        user.ChangeTimeZone(selected.Id, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        await gateway.SendTextMessageAsync(sender.Id, $"Часовой пояс: {selected.Id}. Сохранённые сроки не переносились. /list — проверить записи.", ct);
    }
}
