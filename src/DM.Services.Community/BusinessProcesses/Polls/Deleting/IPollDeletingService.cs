using System;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Polls.Deleting;

/// <summary>
/// Service for poll deletion
/// </summary>
public interface IPollDeletingService
{
    /// <summary>
    /// Delete poll (soft delete)
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    Task Delete(Guid pollId);
}
