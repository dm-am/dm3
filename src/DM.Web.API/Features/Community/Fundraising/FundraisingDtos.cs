using System;

namespace DM.Web.API.Features.Community.Fundraising;

/// <summary>
/// Website fundraising progress
/// </summary>
/// <remarks>
/// Single-row semantics: there is exactly one fundraising goal,
/// it is read by everyone and updated in place by administrators.
/// </remarks>
public class Fundraising
{
    /// <summary>
    /// Target amount to collect
    /// </summary>
    public decimal GoalAmount { get; set; }

    /// <summary>
    /// Amount collected so far
    /// </summary>
    public decimal CollectedAmount { get; set; }

    /// <summary>
    /// Last update timestamp (UTC)
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; set; }
}

/// <summary>
/// Request to update the fundraising progress
/// </summary>
public class UpdateFundraisingRequest
{
    /// <summary>
    /// Target amount to collect (must be greater than 0)
    /// </summary>
    public decimal GoalAmount { get; set; }

    /// <summary>
    /// Amount collected so far (must be 0 or greater)
    /// </summary>
    public decimal CollectedAmount { get; set; }
}
