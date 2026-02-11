using System;
using DM.Services.DataAccess.BusinessObjects.Messaging;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;

/// <summary>
/// Factory for creating chat events
/// </summary>
public interface IGlobalChatEventFactory
{
    /// <summary>
    /// Create chat event entity
    /// </summary>
    /// <param name="createGlobalChatEvent">Create DTO</param>
    /// <param name="userId">Creator user ID</param>
    /// <returns>Chat event entity</returns>
    GlobalChatEvent Create(CreateGlobalChatEvent createGlobalChatEvent, Guid userId);

    /// <summary>
    /// Create chat event participant entity
    /// </summary>
    /// <param name="eventId">Event ID</param>
    /// <param name="userId">User ID</param>
    /// <param name="isOrganizer">Whether the participant is an organizer</param>
    /// <returns>Participant entity</returns>
    GlobalChatEventParticipant CreateParticipant(Guid eventId, Guid userId, bool isOrganizer);
}
