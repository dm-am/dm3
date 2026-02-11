using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Lifecycle;

/// <summary>
/// Service for managing chat event lifecycle (start/end)
/// </summary>
public interface IGlobalChatEventLifecycleService
{
    /// <summary>
    /// Start a scheduled event (transition to Live)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated event</returns>
    Task<GlobalChatEvent> Start(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// End a live event (transition to Ended)
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated event</returns>
    Task<GlobalChatEvent> End(Guid eventId, CancellationToken ct = default);
}
