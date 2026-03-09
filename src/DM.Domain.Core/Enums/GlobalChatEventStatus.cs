namespace DM.Domain.Core.Enums;

/// <summary>
/// Status of an event in global chat
/// </summary>
public enum GlobalChatEventStatus
{
    /// <summary>
    /// Event is scheduled for the future, not yet started
    /// </summary>
    Scheduled = 0,

    /// <summary>
    /// Event is currently active
    /// </summary>
    Live = 1,

    /// <summary>
    /// Event has ended
    /// </summary>
    Ended = 2
}
