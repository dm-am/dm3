namespace DM.Domain.Messaging.Authorization;

/// <summary>
/// List of chat event actions that require authorization
/// </summary>
public enum GlobalChatEventIntention
{
    /// <summary>
    /// Create a new chat event [SeniorModerator+]
    /// </summary>
    Create = 0,

    /// <summary>
    /// Update event details [Organizer]
    /// </summary>
    Update = 1,

    /// <summary>
    /// Delete event [Organizer]
    /// </summary>
    Delete = 2,

    /// <summary>
    /// Start the event (transition to Live) [Organizer]
    /// </summary>
    Start = 3,

    /// <summary>
    /// End the event (transition to Ended) [Organizer]
    /// </summary>
    End = 4,

    /// <summary>
    /// Join open event [Any authenticated user for open events]
    /// </summary>
    Join = 5,

    /// <summary>
    /// Leave event [Participant, not organizer]
    /// </summary>
    Leave = 6,

    /// <summary>
    /// Add participant to closed event [Organizer]
    /// </summary>
    AddParticipant = 7,

    /// <summary>
    /// Remove participant from event [Organizer]
    /// </summary>
    RemoveParticipant = 8
}
