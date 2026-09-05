using System.Globalization;
using System.Text.RegularExpressions;

namespace RoutineEscape.Application.DateTimeResolution;

public sealed partial class RussianDateTimeResolver : IDateTimeResolver
{
    private static readonly Dictionary<string, DayOfWeek> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        ["понедельник"] = DayOfWeek.Monday, ["понедельника"] = DayOfWeek.Monday,
        ["вторник"] = DayOfWeek.Tuesday, ["вторника"] = DayOfWeek.Tuesday,
        ["среду"] = DayOfWeek.Wednesday, ["среда"] = DayOfWeek.Wednesday,
        ["четверг"] = DayOfWeek.Thursday, ["четверга"] = DayOfWeek.Thursday,
        ["пятницу"] = DayOfWeek.Friday, ["пятница"] = DayOfWeek.Friday,
        ["субботу"] = DayOfWeek.Saturday, ["суббота"] = DayOfWeek.Saturday,
        ["воскресенье"] = DayOfWeek.Sunday,
    };

    private static readonly Dictionary<string, int> Months = new(StringComparer.OrdinalIgnoreCase)
    {
        ["января"] = 1, ["февраля"] = 2, ["марта"] = 3, ["апреля"] = 4,
        ["мая"] = 5, ["июня"] = 6, ["июля"] = 7, ["августа"] = 8,
        ["сентября"] = 9, ["октября"] = 10, ["ноября"] = 11, ["декабря"] = 12,
    };

    public DateTimeResolution Resolve(string? dateExpression, string? timeExpression,
        DateTimeOffset nowUtc, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, zone);
        var dateText = Normalize(dateExpression);
        var timeText = Normalize(timeExpression);

        var relative = RelativeRegex().Match(dateText);
        if (relative.Success)
        {
            var amount = int.Parse(relative.Groups[1].Value, CultureInfo.InvariantCulture);
            var unit = relative.Groups[2].Value;
            var value = unit.StartsWith("мин", StringComparison.Ordinal) ? nowUtc.AddMinutes(amount)
                : unit.StartsWith("час", StringComparison.Ordinal) ? nowUtc.AddHours(amount)
                : nowUtc.AddDays(amount);
            return new DateTimeResolution(value);
        }

        var date = ResolveDate(dateText, localNow.Date);
        var time = ResolveTime(timeText);
        if (date is null && time is null)
        {
            throw new FormatException("Date and time expressions are not recognized.");
        }

        var local = (date ?? localNow.Date).Add(time ?? TimeSpan.Zero);
        if (date is null && local <= localNow.DateTime) local = local.AddDays(1);
        if (zone.IsInvalidTime(local)) throw new FormatException("The local time does not exist in this time zone.");
        var ambiguous = zone.IsAmbiguousTime(local);
        var offset = ambiguous ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeResolution(new DateTimeOffset(local, offset), ambiguous);
    }

    private static DateTime? ResolveDate(string text, DateTime today)
    {
        if (text.Contains("послезавтра", StringComparison.Ordinal)) return today.AddDays(2);
        if (text.Contains("завтра", StringComparison.Ordinal)) return today.AddDays(1);
        if (text.Contains("сегодня", StringComparison.Ordinal)) return today;
        foreach (var pair in Weekdays)
        {
            if (!text.Contains(pair.Key, StringComparison.OrdinalIgnoreCase)) continue;
            var days = ((int)pair.Value - (int)today.DayOfWeek + 7) % 7;
            return today.AddDays(days == 0 ? 7 : days);
        }

        var match = CalendarDateRegex().Match(text);
        if (!match.Success || !Months.TryGetValue(match.Groups[2].Value, out var month)) return null;
        var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var candidate = new DateTime(today.Year, month, day);
        return candidate < today ? candidate.AddYears(1) : candidate;
    }

    private static TimeSpan? ResolveTime(string text)
    {
        var match = EveningRegex().Match(text);
        if (match.Success)
        {
            var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            return new TimeSpan(hour < 12 ? hour + 12 : hour, 0, 0);
        }
        match = ClockRegex().Match(text);
        if (match.Success)
            return new TimeSpan(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0, 0);
        if (text.Contains("утром", StringComparison.Ordinal)) return TimeSpan.FromHours(9);
        if (text.Contains("днём", StringComparison.Ordinal) || text.Contains("днем", StringComparison.Ordinal)) return TimeSpan.FromHours(14);
        if (text.Contains("вечером", StringComparison.Ordinal)) return TimeSpan.FromHours(19);
        return null;
    }

    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    [GeneratedRegex(@"через\s+(\d+)\s+(минут\w*|час\w*|дн\w*)", RegexOptions.CultureInvariant)]
    private static partial Regex RelativeRegex();
    [GeneratedRegex(@"\b(\d{1,2})\s+(января|февраля|марта|апреля|мая|июня|июля|августа|сентября|октября|ноября|декабря)\b", RegexOptions.CultureInvariant)]
    private static partial Regex CalendarDateRegex();
    [GeneratedRegex(@"(?:^|\s)(?:в\s*)?(\d{1,2})(?::(\d{2}))?(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ClockRegex();
    [GeneratedRegex(@"(?:^|\s)(?:в\s*)?(\d{1,2})\s+вечера(?:\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex EveningRegex();
}
