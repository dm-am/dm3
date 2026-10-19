namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// How long the text fields of a chat may be, on creation and on editing alike.
/// </summary>
internal static class ChatFieldLimits
{
    public const int TitleMaxLength = 200;

    /// <summary>
    /// The most participants a group chat is created with.
    /// </summary>
    public const int MaxParticipants = 50;
}
