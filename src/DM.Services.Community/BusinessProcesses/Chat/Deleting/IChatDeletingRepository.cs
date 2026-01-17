using System;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Chat.Deleting;

/// <summary>
/// Repository for deleting chat messages
/// </summary>
internal interface IChatDeletingRepository
{
    /// <summary>
    /// Mark chat message as deleted (soft delete)
    /// </summary>
    /// <param name="id">Message identifier</param>
    Task Delete(Guid id);
}
