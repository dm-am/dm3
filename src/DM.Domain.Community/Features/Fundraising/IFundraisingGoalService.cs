using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Fundraising;

public interface IFundraisingGoalService
{
    /// <returns>Fundraising goal or throws if not found</returns>
    Task<FundraisingGoal> GetAsync();

    /// <summary>
    /// Update the fundraising goal (single-row semantics)
    /// </summary>
    Task<FundraisingGoal> UpdateAsync(UpdateFundraisingGoal updateGoal);
}
