using System;

namespace DM.Web.API.Features.Game.Rooms;

/// <summary>
/// Request model for waiting for a post in a room ("Ждем пост")
/// </summary>
/// <remarks>
/// The character is the whole of what the caller says. Who waits is the caller,
/// read from the session; who is waited for is the player of that character,
/// read from the room.
/// </remarks>
public class CreatePostPendencyRequest
{
    /// <summary>
    /// Character whose turn it is to post
    /// </summary>
    public Guid CharacterId { get; set; }
}
