using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL model for the website fundraising progress
/// </summary>
/// <remarks>
/// Single-row semantics: the table holds exactly one row,
/// seeded with a fixed identifier and updated in place.
/// </remarks>
[Table("FundraisingGoals")]
public class FundraisingGoal
{
    /// <summary>
    /// Fundraising goal identifier
    /// </summary>
    [Key]
    public Guid FundraisingGoalId { get; set; }

    /// <summary>
    /// What the money is being collected for, shown above the progress bar
    /// </summary>
    [Required]
    [MaxLength(200)]
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

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    public Guid? UpdatedByUserId { get; set; }

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(UpdatedByUserId))]
    public virtual User? UpdatedBy { get; set; }
}
