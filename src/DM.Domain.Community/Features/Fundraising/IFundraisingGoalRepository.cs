using System;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Fundraising;

/// <summary>
/// Storage for the fundraising goal (single-row table)
/// </summary>
public interface IFundraisingGoalRepository
{
    /// <returns>Fundraising goal or null if the row is missing</returns>
    Task<FundraisingGoal?> Get();

    /// <summary>
    /// Update the fundraising goal in place
    /// </summary>
    Task<FundraisingGoal> Update(UpdateFundraisingGoal update, Guid updatedByUserId, DateTimeOffset modifiedUtc);
}
