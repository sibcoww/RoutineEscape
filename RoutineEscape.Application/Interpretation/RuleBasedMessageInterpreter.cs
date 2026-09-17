using System.Text.RegularExpressions;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.Application.Interpretation;

/// <summary>Conservative Russian rules; confidence indicates a rule match, not a probability.</summary>
public sealed class RuleBasedMessageInterpreter : IMessageInterpreter
{
    private const string Relative = @"\bчерез\s+\d+\s+(?:минут\w*|час\w*|дн\w*)\b";
    private const string Date = @"\b(?:сегодня|послезавтра|завтра|(?:в|до)\s+(?:следующ\w+\s+)?(?:понедельник\w*|вторник\w*|сред[ауы]|четверг\w*|пятниц[уыа]|суббот[уыа]|воскресень[ея])|\d{1,2}\s+(?:января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря)(?:\s+\d{4})?|\d{1,2}(?:(?:-го|\s*го)(?:\s+числа)?|\s+числа)|\d{1,2}\.\d{2}(?:\.\d{4})?)\b";
    private const string Time = @"\b(?:в\s+\d+(?::\d+)?(?:\s+(?:утра|дня|вечера|ночи))?|\d+:\d+|утром|днём|днем|вечером)\b";
    private static readonly string[] Hours = ["ноль", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять", "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать", "двадцать", "двадцать один", "двадцать два", "двадцать три"];

    public Task<MessageInterpretation> InterpretAsync(string message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        cancellationToken.ThrowIfCancellationRequested();
        var text = message.Trim().ToLowerInvariant();
        var normalized = text;
        for (var hour = Hours.Length - 1; hour >= 0; hour--)
            normalized = Regex.Replace(normalized, $@"\bв\s+{Hours[hour]}\s+ноль\s+ноль\b", $"в {hour:00}:00", RegexOptions.CultureInvariant);

        var dateMatches = Matches(normalized, $"{Relative}|{Date}");
        var timeMatches = Matches(normalized, Time);
        string? date = dateMatches.Length == 0 ? null : dateMatches.Length == 1 ? dateMatches[0] : "неоднозначная дата";
        string? time = timeMatches.Length == 0 ? null : timeMatches.Length == 1 ? timeMatches[0] : "неоднозначное время";
        // Preserve unsupported temporal input for validation instead of silently discarding it.
        if (date is null && Has(normalized, @"\b(?:через|на следующей|на следующем)\b")) date = "неизвестная дата";
        if (time is null && Has(normalized, @"\bв\s+(?:пол\w*|девятнадцать|двадцать|семь|восемь|девять)\b")) time = "неизвестное время";

        var reminder = Has(text, @"\b(?:напомни\w*|напоминание)\b");
        var eventMatch = Has(text, @"\b(?:созвон\w*|встреча|встречу|собеседовани\w*|совещани\w*|вебинар\w*|концерт\w*|при[её]м у врача)\b");
        var task = Has(text, @"\b(?:купить|отправить|сдать|оплатить|позвонить|подготовить|сделать|забрать|написать|проверить|выключить|записаться|задача|нужно|надо)\b");
        var note = Has(text, @"\b(?:заметка|запомни|номер аудитории|адрес|рецепт|ссылка|пароль)\b") || text.StartsWith("https://", StringComparison.Ordinal);
        var negated = Has(text, @"\b(?:не|отмен[аеы]\w*|отменить)\b");
        var intent = negated ? Intent.Unknown
            : note && !reminder && !task && !eventMatch ? Intent.Note
            : reminder && !note ? Intent.Reminder
            : task && !eventMatch && !note ? (date is not null && Has(date, Relative) ? Intent.Reminder : Intent.Task)
            : eventMatch && !task && !note ? Intent.Event
            : Intent.Unknown;
        return Task.FromResult(new MessageInterpretation(intent, intent == Intent.Unknown ? 0m : 0.9m,
            message.Trim(), null, date, time, null, null));
    }

    private static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static string[] Matches(string text, string pattern) => Regex.Matches(text, pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking).Select(match => match.Value).ToArray();
}
