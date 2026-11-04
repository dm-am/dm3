namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// How long the text fields of a global chat event may be, on creation and on editing alike.
/// </summary>
internal static class GlobalChatEventFieldLimits
{
    public const int TitleMaxLength = 200;

    public const int DescriptionMaxLength = 10000;
}
