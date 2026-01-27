using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Polls.Reading;

namespace DM.Services.Community.BusinessProcesses.Polls.Updating;

/// <summary>
/// Service for poll updating
/// </summary>
public interface IPollUpdatingService
{
    /// <summary>
    /// Update existing poll
    /// </summary>
    /// <param name="updatePoll"></param>
    /// <returns></returns>
    Task<Poll> Update(UpdatePoll updatePoll);
}
