namespace DM.Domain.Messaging.Features.Chats;

/// <summary>
/// How long the text fields of a chat may be.
/// </summary>
/// <remarks>
/// One declaration because creation and editing must agree. They already
/// disagreed once, for games: the create validator read a shared constant while
/// the edit validator kept its own numbers, so a title creation accepted could
/// not be saved again after any change to the row.
/// </remarks>
internal static class ChatFieldLimits
{
    /// <summary>Longest chat title.</summary>
    public const int TitleMaxLength = 200;
}
