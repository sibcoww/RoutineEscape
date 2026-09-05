namespace RoutineEscape.Domain.Enums;

public enum MessageSourceType
{
    Direct = 0,
    ForwardedUser = 1,
    ForwardedHiddenUser = 2,
    ForwardedChat = 3,
    ForwardedChannel = 4,
}
