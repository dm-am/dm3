using System.Threading.Tasks;
using DalVote = DM.Services.DataAccess.BusinessObjects.Games.Rating.Vote;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Creating;

/// <summary>
/// Repository for creating votes
/// </summary>
public interface IVoteCreatingRepository
{
    /// <summary>
    /// Create new vote
    /// </summary>
    /// <param name="vote">Vote DAL model</param>
    /// <returns>Created vote</returns>
    Task<DalVote> Create(DalVote vote);
}
