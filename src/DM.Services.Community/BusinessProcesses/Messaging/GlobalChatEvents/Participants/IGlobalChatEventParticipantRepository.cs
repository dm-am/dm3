using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DbGlobalChatEventParticipant = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEventParticipant;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Participants;

/// <summary>
/// Repository for managing chat event participants
/// </summary>
public interface IGlobalChatEventParticipantRepository
{
    /// <summary>
    /// Get participants of an event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <returns>List of participants</returns>
    Task<IEnumerable<GlobalChatEventParticipant>> GetParticipants(Guid eventId);

    /// <summary>
    /// Add participant to event
    /// </summary>
    /// <param name="participant">Participant entity</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created participant</returns>
    Task<GlobalChatEventParticipant> Add(
        DbGlobalChatEventParticipant participant,
        CancellationToken ct = default);

    /// <summary>
    /// Remove participant from event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="userId">User identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task Remove(Guid eventId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Check if user is participant of the event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="userId">User identifier</param>
    /// <returns>True if user is participant</returns>
    Task<bool> IsParticipant(Guid eventId, Guid userId);
}
