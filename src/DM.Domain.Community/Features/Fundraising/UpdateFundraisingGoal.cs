namespace DM.Domain.Community.Features.Fundraising;

public class UpdateFundraisingGoal
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
}
