using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Lifecycle;

/// <summary>
/// Repository for managing chat event lifecycle
/// </summary>
public interface IGlobalChatEventLifecycleRepository
{
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
}
