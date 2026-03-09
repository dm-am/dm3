using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// Unified service for global chat events: CRUD, lifecycle, and participants
/// </summary>
public interface IGlobalChatEventService
{
    // ═══ CREATE ═══

    /// <summary>
    /// Create a new chat event
    /// </summary>
    /// <param name="createGlobalChatEvent">Creation DTO</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created event</returns>
    Task<GlobalChatEvent> CreateAsync(CreateGlobalChatEvent createGlobalChatEvent, CancellationToken ct = default);

    // ═══ READ ═══

    /// <summary>
    /// Get chat event by ID
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>Chat event</returns>
    /// <exception cref="DM.Domain.Core.Exceptions.HttpException">If event not found</exception>
    Task<GlobalChatEvent> GetAsync(Guid eventId);

    /// <summary>
    /// Get currently active (Live) event or null
    /// </summary>
    /// <returns>Live event or null</returns>
    Task<GlobalChatEvent?> GetActiveEventAsync();

    /// <summary>
    /// Get upcoming (Scheduled) events
    /// </summary>
    /// <returns>List of scheduled events</returns>
    Task<IEnumerable<GlobalChatEvent>> GetUpcomingEventsAsync();

    /// <summary>
    /// Get all non-ended events (Live + Scheduled)
    /// </summary>
    /// <returns>List of events</returns>
    Task<IEnumerable<GlobalChatEvent>> GetAllAsync();

    // ═══ UPDATE ═══

    /// <summary>
    /// Update a chat event
    /// </summary>
    /// <param name="updateGlobalChatEvent">Update DTO</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated event</returns>
    Task<GlobalChatEvent> UpdateAsync(UpdateGlobalChatEvent updateGlobalChatEvent, CancellationToken ct = default);

    // ═══ DELETE ═══

    /// <summary>
    /// Delete a chat event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteAsync(Guid eventId, CancellationToken ct = default);

    // ═══ LIFECYCLE ═══

    /// <summary>
    /// Start a scheduled event (transition to Live)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated event</returns>
    Task<GlobalChatEvent> StartAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// End a live event (transition to Ended)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated event</returns>
    Task<GlobalChatEvent> EndAsync(Guid eventId, CancellationToken ct = default);

    // ═══ PARTICIPANTS ═══

    /// <summary>
    /// Get participants of an event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>List of participants</returns>
    Task<IEnumerable<GlobalChatEventParticipant>> GetParticipantsAsync(Guid eventId);

    /// <summary>
    /// Join an open event (self-service for users)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created participant</returns>
    Task<GlobalChatEventParticipant> JoinAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Leave an event (self-service for participants)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task LeaveAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Add participant to event (organizer action)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="userId">User identifier to add</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created participant</returns>
    Task<GlobalChatEventParticipant> AddParticipantAsync(Guid eventId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Remove participant from event (organizer action)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="userId">User identifier to remove</param>
    /// <param name="ct">Cancellation token</param>
    Task RemoveParticipantAsync(Guid eventId, Guid userId, CancellationToken ct = default);
}
