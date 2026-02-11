using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Deleting;

/// <summary>
/// Service for deleting chat events
/// </summary>
public interface IGlobalChatEventDeletingService
{
    /// <summary>
    /// Delete a chat event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task Delete(Guid eventId, CancellationToken ct = default);
}
