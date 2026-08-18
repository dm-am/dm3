using System;

namespace DM.Domain.Community.Features.Fundraising;

/// <summary>
/// Domain DTO for the website fundraising progress
/// </summary>
/// <remarks>
/// Single-row semantics: there is exactly one fundraising goal,
/// seeded with a fixed identifier and updated in place.
/// </remarks>
public class FundraisingGoal
{
    /// <summary>
    /// What the money is being collected for
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Target amount to collect
    /// </summary>
    public decimal GoalAmount { get; set; }

    /// <summary>
    /// Amount collected so far
    /// </summary>
    public decimal CollectedAmount { get; set; }

    /// <summary>
    /// Last update moment (UTC)
    /// </summary>
    public DateTimeOffset ModifiedUtc { get; set; }
}
