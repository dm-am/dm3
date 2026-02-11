using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess.RelationalStorage;
using DbGlobalChatEvent = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEvent;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Updating;

/// <summary>
/// Repository for updating chat events
/// </summary>
public interface IGlobalChatEventUpdatingRepository
{
    /// <summary>
    /// Update chat event
    /// </summary>
    /// <param name="updateBuilder">Update builder</param>
    /// <param name="ct">Cancellation token</param>
    Task Update(IUpdateBuilder<DbGlobalChatEvent> updateBuilder, CancellationToken ct = default);
}
