using System;
using System.Threading.Tasks;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Deleting;

/// <summary>
/// Repository for deleting votes
/// </summary>
public interface IVoteDeletingRepository
{
    /// <summary>
    /// Delete vote by identifier
    /// </summary>
    /// <param name="voteId">Vote identifier</param>
    /// <returns>Task</returns>
    Task Delete(Guid voteId);
}
