using RoutineEscape.Application.Interpretation;
using RoutineEscape.Domain.Enums;

namespace RoutineEscape.UnitTests.Interpretation;

public sealed class RuleBasedMessageInterpreterTests
{
    [Theory]
    [InlineData("созвон завтра в 19:00", Intent.Event, "завтра", "в 19:00")]
    [InlineData("Созвон завтра в девятнадцать ноль ноль", Intent.Event, "завтра", "в 19:00")]
    [InlineData("собеседование в пятницу в 7 вечера", Intent.Event, "в пятницу", "в 7 вечера")]
    [InlineData("напомни через 40 минут выключить духовку", Intent.Reminder, "через 40 минут", null)]
    [InlineData("через 2 часа позвонить маме", Intent.Reminder, "через 2 часа", null)]
    [InlineData("отправить документы до среды вечером", Intent.Task, "до среды", "вечером")]
    [InlineData("купить молоко", Intent.Task, null, null)]
    [InlineData("номер аудитории — 305", Intent.Note, null, null)]
    [InlineData("заметка: интересная книга", Intent.Note, null, null)]
    [InlineData("завтра универ", Intent.Unknown, "завтра", null)]
    [InlineData("подготовить встречу завтра", Intent.Unknown, "завтра", null)]
    [InlineData("не надо созвон завтра", Intent.Unknown, "завтра", null)]
    [InlineData("привет", Intent.Unknown, null, null)]
    [InlineData("созвон завтра или послезавтра в 19:00", Intent.Event, "неоднозначная дата", "в 19:00")]
    public async Task Interpret_UsesConservativeRules(string text, Intent intent, string? date, string? time)
    {
        var result = await new RuleBasedMessageInterpreter().InterpretAsync(text, CancellationToken.None);
        Assert.Equal(intent, result.Intent);
        Assert.Equal(date, result.DateExpression);
        Assert.Equal(time, result.TimeExpression);
        Assert.Equal(text, result.Title);
        Assert.Equal(intent == Intent.Unknown ? 0m : 0.9m, result.Confidence);
    }
}
