using RoutineEscape.Application.DateTimeResolution;

namespace RoutineEscape.UnitTests.DateTimeResolution;

public sealed class RussianDateTimeResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 5, 6, 0, 0, TimeSpan.Zero);
    private readonly RussianDateTimeResolver _resolver = new();

    [Theory]
    [InlineData("23го")]
    [InlineData("23-го")]
    [InlineData("23 го")]
    [InlineData("23 числа")]
    [InlineData("23го числа")]
    public void DayOnly_UsesCurrentLocalMonth(string date)
    {
        var now = DateTimeOffset.Parse("2026-09-16T10:00:00Z");
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T08:00:00Z"),
            _resolver.Resolve(date, "13:00", now, "Asia/Qyzylorda").Value);
    }

    [Theory]
    [InlineData("15го")]
    [InlineData("16-го")]
    [InlineData("31 числа")]
    [InlineData("0 го")]
    public void DayOnly_RejectsPastOrInvalidWithoutRollover(string date) =>
        Assert.Throws<FormatException>(() => _resolver.Resolve(date, "13:00",
            DateTimeOffset.Parse("2026-09-16T10:00:00Z"), "Asia/Qyzylorda"));

    [Fact]
    public void DayOnly_MonthComesFromUserZoneAtUtcMonthBoundary()
    {
        var now = DateTimeOffset.Parse("2026-08-31T22:00:00Z");
        Assert.Equal(DateTimeOffset.Parse("2026-09-23T10:00:00Z"),
            _resolver.Resolve("23го", "13:00", now, "Europe/Moscow").Value);
    }

    [Theory]
    [InlineData("завтра", null, "2026-09-06T00:00:00+05:00")]
    [InlineData("через 2 часа", null, "2026-09-05T08:00:00+00:00")]
    [InlineData("в пятницу", null, "2026-09-11T00:00:00+05:00")]
    [InlineData("12 сентября", null, "2026-09-12T00:00:00+05:00")]
    [InlineData("завтра", "в 7 вечера", "2026-09-06T19:00:00+05:00")]
    public void Resolve_ConvertsRequiredRussianExpressions(string date, string? time, string expected)
    {
        var result = _resolver.Resolve(date, time, Now, "Asia/Qyzylorda");
        Assert.Equal(DateTimeOffset.Parse(expected), result.Value);
        Assert.False(result.IsAmbiguous);
    }

    [Fact]
    public void Resolve_TimeOnlyInPast_UsesNextLocalDay()
    {
        var result = _resolver.Resolve(null, "в 7 утра", Now, "Asia/Qyzylorda");
        Assert.Equal(DateTimeOffset.Parse("2026-09-06T07:00:00+05:00"), result.Value);
    }

    [Fact]
    public void Resolve_RejectsUnknownExpression() =>
        Assert.Throws<FormatException>(() => _resolver.Resolve("когда-нибудь", null, Now, "Asia/Qyzylorda"));

    [Theory]
    [InlineData("завтра", "25:00")]
    [InlineData("завтра", "19:75")]
    [InlineData("завтра", "неизвестное время")]
    [InlineData("в следующую пятницу", "19:00")]
    [InlineData("12 сентября 2027", "19:00")]
    [InlineData("неизвестная дата", "19:00")]
    [InlineData("через 2 часа", "19:00")]
    public void Resolve_DoesNotGuessInvalidOrPartiallySupportedDates(string date, string time) =>
        Assert.Throws<FormatException>(() => _resolver.Resolve(date, time, Now, "Asia/Qyzylorda"));
}
