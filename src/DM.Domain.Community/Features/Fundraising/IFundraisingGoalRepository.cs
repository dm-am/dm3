using System;
using System.Threading.Tasks;

namespace DM.Domain.Community.Features.Fundraising;

/// <summary>
/// Storage for the fundraising goal (single-row table)
/// </summary>
public interface IFundraisingGoalRepository
{
    // READ

    /// <summary>
    /// Get the fundraising goal
    /// </summary>
    /// <returns>Fundraising goal or null if the row is missing</returns>
    Task<FundraisingGoal?> Get();

    // WRITE

    /// <summary>
    /// Update the fundraising goal in place
    /// </summary>
    /// <param name="update">Update data</param>
    /// <param name="updatedByUserId">User who performed the update</param>
    /// <param name="modifiedUtc">Update moment (UTC)</param>
    /// <returns>Updated fundraising goal</returns>
    Task<FundraisingGoal> Update(UpdateFundraisingGoal update, Guid updatedByUserId, DateTimeOffset modifiedUtc);
}
