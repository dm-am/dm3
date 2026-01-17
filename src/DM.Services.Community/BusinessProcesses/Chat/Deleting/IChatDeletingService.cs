using System;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Chat.Deleting;

/// <summary>
/// Service for deleting chat messages
/// </summary>
public interface IChatDeletingService
{
    /// <summary>
    /// Delete chat message (soft delete)
    /// </summary>
    /// <param name="id">Message identifier</param>
    Task Delete(Guid id);
}
