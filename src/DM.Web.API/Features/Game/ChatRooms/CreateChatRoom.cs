namespace DM.Web.API.Features.Game.ChatRooms;

/// <summary>
/// Request model for creating a chat room
/// </summary>
public class CreateChatRoom
{
    /// <summary>
    /// Chat room title
    /// </summary>
    public string Title { get; set; } = null!;
}
