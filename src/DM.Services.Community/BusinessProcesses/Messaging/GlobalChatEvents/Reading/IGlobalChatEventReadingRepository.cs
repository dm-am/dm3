using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

/// <summary>
/// Repository for reading chat events
/// </summary>
public interface IGlobalChatEventReadingRepository
{
    /// <summary>
    /// Get chat event by ID
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>Chat event or null</returns>
    Task<GlobalChatEvent?> Get(Guid eventId);

    /// <summary>
    /// Get list of chat events by status
    /// </summary>
    /// <param name="statuses">Event statuses to filter by</param>
    /// <returns>List of chat events</returns>
    Task<IEnumerable<GlobalChatEvent>> GetByStatus(params GlobalChatEventStatus[] statuses);

    /// <summary>
    /// Get currently active (Live) event
    /// </summary>
    /// <returns>Live event or null</returns>
    Task<GlobalChatEvent?> GetActiveEvent();

    /// <summary>
    /// Get upcoming (Scheduled) events
    /// </summary>
    /// <returns>List of scheduled events ordered by start date</returns>
    Task<IEnumerable<GlobalChatEvent>> GetUpcomingEvents();

    /// <summary>
    /// Check if there is an active (Live) event
    /// </summary>
    /// <returns>True if there is an active event</returns>
    Task<bool> HasActiveEvent();

    /// <summary>
    /// Check if user is participant of the event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns>True if user is participant</returns>
    Task<bool> IsParticipant(Guid eventId, Guid userId);
}
