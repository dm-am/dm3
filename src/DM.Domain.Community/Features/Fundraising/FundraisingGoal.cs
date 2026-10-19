using System;

namespace DM.Domain.Community.Features.Fundraising;

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

    public decimal GoalAmount { get; set; }

    /// <summary>
    /// Amount collected so far
    /// </summary>
    public decimal CollectedAmount { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }
}
