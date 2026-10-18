namespace DM.Domain.Messaging.Authorization;

/// <summary>
/// List of chat actions that require authorization
/// </summary>
public enum ChatIntention
{
    /// <summary>
    /// Create new message
    /// </summary>
    CreateMessage = 0,

    /// <summary>
    /// Update chat (title, participants)
    /// </summary>
    UpdateChat = 1,

    /// <summary>
    /// Delete chat (group chats only)
    /// </summary>
    DeleteChat = 2,

    /// <summary>
    /// Leave the chat (group chats only)
    /// </summary>
    LeaveChat = 3,

    /// <summary>
    /// Clear the history of the chat for oneself (direct and group chats)
    /// </summary>
    ClearHistory = 4
}
