using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Deleting;

/// <summary>
/// Repository for deleting chat events
/// </summary>
public interface IGlobalChatEventDeletingRepository
{
    /// <summary>
    /// Delete chat event
    /// </summary>
    /// <param name="eventId">Event identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task Delete(Guid eventId, CancellationToken ct = default);
}
