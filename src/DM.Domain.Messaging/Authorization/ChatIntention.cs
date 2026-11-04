namespace DM.Domain.Messaging.Authorization;

public enum ChatIntention
{
    CreateMessage = 0,

    UpdateChat = 1,

    DeleteChat = 2,

    LeaveChat = 3,

    /// <summary>
    /// Clear the history of the chat for oneself
    /// </summary>
    ClearHistory = 4
}
