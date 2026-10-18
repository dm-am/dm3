namespace DM.Web.API.Features.Messaging.Chats;

/// <summary>
/// Number of unread messages across every conversation of the current user
/// </summary>
/// <remarks>
/// Shaped after NotificationCount: the header carries two badges answering the
/// same question about two kinds of thing, and reading one of them out of a
/// counter and the other out of a page of rows is what let them disagree.
/// Unlike that one it travels in an Envelope. The bare body of the notification
/// counter is inherited debt the EnvelopeCoverageShould list may only shrink,
/// and a new endpoint does not join it.
/// </remarks>
public class ChatUnreadCount
{
    /// <summary>
    /// Number of unread messages
    /// </summary>
    public long Count { get; set; }
}
