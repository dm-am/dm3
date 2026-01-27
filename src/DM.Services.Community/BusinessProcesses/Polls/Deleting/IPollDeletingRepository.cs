using System;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Polls.Deleting;

/// <summary>
/// Poll deleting storage
/// </summary>
internal interface IPollDeletingRepository
{
    /// <summary>
    /// Mark poll as removed
    /// </summary>
    /// <param name="pollId">Poll identifier</param>
    Task Delete(Guid pollId);
}
