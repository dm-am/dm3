using System;

namespace DM.Domain.Core.Chats;

/// <summary>
/// Chats the product names rather than creates.
/// </summary>
/// <remarks>
/// The global chat is a single row that exists because the product says it does,
/// and everything addresses it by identifier. That makes the identifier a product
/// fact. It used to be declared on the EF entity, where it was out of reach of
/// every service that does not reference persistence, and the API layer had to
/// import an entity to spell it.
/// </remarks>
public static class WellKnownChats
{
    /// <summary>
    /// Identifier of the one global chat.
    /// </summary>
    public static readonly Guid GlobalChatId = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
