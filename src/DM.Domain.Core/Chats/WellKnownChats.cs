namespace DM.Domain.Core.Chats;

/// <summary>
/// Chats the product names rather than creates.
/// </summary>
/// <remarks>
/// The global chat is a single row addressed by identifier, so the identifier is a
/// product fact.
/// </remarks>
public static class WellKnownChats
{
    /// <summary>
    /// Identifier of the one global chat.
    /// </summary>
    public static readonly Guid GlobalChatId = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
