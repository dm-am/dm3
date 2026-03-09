namespace DM.Web.API.Features.Messaging.Chats;

/// <summary>
/// Result of chat availability check
/// </summary>
/// <remarks>
/// Used to determine if current user can start a chat with another user.
/// This check considers both users' blacklists.
/// </remarks>
public class ChatAvailability
{
    /// <summary>
    /// Whether chat can be started
    /// </summary>
    /// <remarks>
    /// False if either user has blocked the other.
    /// </remarks>
    public bool CanStart { get; set; }

    /// <summary>
    /// Reason why chat cannot be started (null if can start)
    /// </summary>
    /// <remarks>
    /// Possible values:
    /// - YouBlockedThem: You have blocked this user
    /// - CannotCommunicate: Communication is not possible (privacy-protected reason)
    /// </remarks>
    public string? Reason { get; set; }
}
