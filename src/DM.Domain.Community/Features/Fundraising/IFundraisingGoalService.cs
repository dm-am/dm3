using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Fundraising;

/// <summary>
/// Service for fundraising goal operations
/// </summary>
public interface IFundraisingGoalService
{
    /// <summary>
    /// Get the current fundraising goal
    /// </summary>
    /// <returns>Fundraising goal or throws if not found</returns>
    Task<FundraisingGoal> GetAsync();

    /// <summary>
    /// Update the fundraising goal (single-row semantics)
    /// </summary>
    /// <param name="updateGoal">Update data</param>
    /// <returns>Updated fundraising goal</returns>
    Task<FundraisingGoal> UpdateAsync(UpdateFundraisingGoal updateGoal);
}
