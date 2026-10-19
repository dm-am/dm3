namespace DM.Domain.Game.Authorization;

public enum RoomIntention
{
    CreatePost = 1,

    CreatePostPendency = 2,

    DeletePostPendency = 3,

    ViewMessages = 4,

    SendMessage = 5
}
