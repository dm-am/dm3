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
    UpdateChat = 1
}
