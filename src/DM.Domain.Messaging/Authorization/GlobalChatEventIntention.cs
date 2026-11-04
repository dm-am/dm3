namespace DM.Domain.Messaging.Authorization;

public enum GlobalChatEventIntention
{
    Create = 0,

    Update = 1,

    Delete = 2,

    Start = 3,

    End = 4,

    Join = 5,

    Leave = 6,

    AddParticipant = 7,

    RemoveParticipant = 8
}
