using System;
using System.Threading.Tasks;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Deleting;

/// <summary>
/// Service for deleting votes
/// </summary>
public interface IVoteDeletingService
{
    /// <summary>
    /// Delete existing vote
    /// </summary>
    /// <param name="voteId">Vote identifier</param>
    /// <returns>Task</returns>
    Task Delete(Guid voteId);
}
