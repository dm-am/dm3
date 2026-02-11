using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

/// <summary>
/// Service for reading chat events
/// </summary>
public interface IGlobalChatEventReadingService
{
    /// <summary>
    /// Get chat event by ID
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>Chat event</returns>
    /// <exception cref="DM.Services.Core.Exceptions.HttpException">If event not found</exception>
    Task<GlobalChatEvent> Get(Guid eventId);

    /// <summary>
    /// Get currently active (Live) event or null
    /// </summary>
    /// <returns>Live event or null</returns>
    Task<GlobalChatEvent?> GetActiveEvent();

    /// <summary>
    /// Get upcoming (Scheduled) events
    /// </summary>
    /// <returns>List of scheduled events</returns>
    Task<IEnumerable<GlobalChatEvent>> GetUpcomingEvents();

    /// <summary>
    /// Get all non-ended events (Live + Scheduled)
    /// </summary>
    /// <returns>List of events</returns>
    Task<IEnumerable<GlobalChatEvent>> GetAll();
}
