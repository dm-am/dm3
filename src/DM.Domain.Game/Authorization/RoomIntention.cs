namespace DM.Domain.Game.Authorization;

/// <summary>
/// Room actions that require authorization
/// </summary>
public enum RoomIntention
{
    /// <summary>
    /// Create new post
    /// </summary>
    CreatePost = 1,

    /// <summary>
    /// Create new post pendency
    /// </summary>
    CreatePostPendency = 2,

    /// <summary>
    /// Delete post pendency
    /// </summary>
    DeletePostPendency = 3
}
