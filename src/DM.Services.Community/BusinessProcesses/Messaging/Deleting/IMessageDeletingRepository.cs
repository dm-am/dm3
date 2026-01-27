using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Messaging.Deleting;

/// <summary>
/// Storage for deleting messages
/// </summary>
internal interface IMessageDeletingRepository
{
    /// <summary>
    /// Delete message (soft delete)
    /// </summary>
    /// <param name="messageId">Message identifier</param>
    /// <param name="deletedByUserId">User who deleted the message</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task Delete(Guid messageId, Guid deletedByUserId, CancellationToken ct = default);
}
