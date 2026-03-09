using System;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// Factory for creating chat event data
/// </summary>
public interface IGlobalChatEventFactory
{
    /// <summary>
    /// Create chat event data
    /// </summary>
    /// <param name="createGlobalChatEvent">Create DTO</param>
    /// <param name="userId">Creator user ID</param>
    /// <returns>Chat event data</returns>
    CreateGlobalChatEventEntity Create(CreateGlobalChatEvent createGlobalChatEvent, Guid userId);

    /// <summary>
    /// Create chat event participant data
    /// </summary>
    /// <param name="eventId">Event ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="isOrganizer">Whether the participant is an organizer</param>
    /// <returns>Participant data</returns>
    CreateGlobalChatEventParticipantEntity CreateParticipant(Guid eventId, Guid userId, bool isOrganizer);
}
