using System.Threading.Tasks;
using DM.Services.Gaming.Dto.Input;
using DM.Services.Gaming.Dto.Output;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Creating;

/// <summary>
/// Service for creating votes
/// </summary>
public interface IVoteCreatingService
{
    /// <summary>
    /// Create new vote on a post
    /// </summary>
    /// <param name="createVote">Vote creation data</param>
    /// <returns>Created vote</returns>
    Task<Vote> Create(CreateVote createVote);
}
