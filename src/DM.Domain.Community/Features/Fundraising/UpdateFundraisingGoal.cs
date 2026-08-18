namespace DM.Domain.Community.Features.Fundraising;

/// <summary>
/// DTO model for fundraising goal update
/// </summary>
public class UpdateFundraisingGoal
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
}
