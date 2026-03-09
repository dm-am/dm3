using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Messaging.Features.GlobalChatEvents;

/// <summary>
/// Unified repository for chat event operations
/// </summary>
public interface IGlobalChatEventRepository
{
    // ═══ READ ═══

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

    // ═══ WRITE ═══

    /// <summary>
    /// Create a chat event with the creator as initial participant
    /// </summary>
    /// <param name="chatEvent">Event data</param>
    /// <param name="creatorParticipant">Creator participant data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created event</returns>
    Task<GlobalChatEvent> Create(
        CreateGlobalChatEventEntity chatEvent,
        CreateGlobalChatEventParticipantEntity creatorParticipant,
        CancellationToken ct = default);

    /// <summary>
    /// Update chat event
    /// </summary>
    /// <param name="update">Update data</param>
    /// <param name="ct">Cancellation token</param>
    Task Update(UpdateGlobalChatEventEntity update, CancellationToken ct = default);

    /// <summary>
    /// Delete chat event and its participants
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task Delete(Guid eventId, CancellationToken ct = default);

    // ═══ LIFECYCLE ═══

    /// <summary>
    /// Update event status
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="status">New status</param>
    /// <param name="startedAt">Started timestamp (for Live transition)</param>
    /// <param name="endedAt">Ended timestamp (for Ended transition)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated event</returns>
    Task<GlobalChatEvent> UpdateStatus(
        Guid eventId,
        GlobalChatEventStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        CancellationToken ct = default);

    // ═══ PARTICIPANTS ═══

    /// <summary>
    /// Get participants of an event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>List of participants</returns>
    Task<IEnumerable<GlobalChatEventParticipant>> GetParticipants(Guid eventId);

    /// <summary>
    /// Add participant to event
    /// </summary>
    /// <param name="participant">Participant data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created participant</returns>
    Task<GlobalChatEventParticipant> AddParticipant(
        CreateGlobalChatEventParticipantEntity participant,
        CancellationToken ct = default);

    /// <summary>
    /// Remove participant from event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task RemoveParticipant(Guid eventId, Guid userId, CancellationToken ct = default);
}
