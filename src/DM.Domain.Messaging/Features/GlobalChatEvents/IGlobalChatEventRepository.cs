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
    /// Move an event from one status to the next
    /// </summary>
    /// <remarks>
    /// The status the move starts from is carried into the write, so a move that
    /// somebody else made a moment earlier is not made a second time: the caller
    /// is told it changed nothing rather than stamping its own moment over theirs
    /// and announcing the same transition again. Only the timestamps a move
    /// carries are written; the others keep what they hold.
    /// </remarks>
    /// <param name="eventId">Event identifier</param>
    /// <param name="expectedStatus">Status the event has to be in for the move to happen</param>
    /// <param name="status">New status</param>
    /// <param name="startedAt">Started timestamp (for Live transition)</param>
    /// <param name="endedAt">Ended timestamp (for Ended transition)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated event, or null when the event was no longer in the expected status</returns>
    /// <exception cref="DM.Domain.Core.Exceptions.DuplicateEntityException">
    /// Another event holds the live status this move asks for
    /// </exception>
    Task<GlobalChatEvent?> UpdateStatus(
        Guid eventId,
        GlobalChatEventStatus expectedStatus,
        GlobalChatEventStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        CancellationToken ct = default);

    // ═══ PARTICIPANTS ═══

    /// <summary>
    /// Add participant to event
    /// </summary>
    /// <remarks>
    /// One row per (event, person), settled by the write itself: the answer is
    /// null when the list already carried this person, whether it did before the
    /// call or acquired them while it ran.
    /// </remarks>
    /// <param name="participant">Participant data</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created participant, or null when the person was already on the list</returns>
    Task<GlobalChatEventParticipant?> AddParticipant(
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
