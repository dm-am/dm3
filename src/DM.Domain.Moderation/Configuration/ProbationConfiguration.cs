using DM.Domain.Core.Configuration;

namespace DM.Domain.Moderation.Configuration;

/// <summary>
/// Configuration for user probation (newbie) thresholds
/// </summary>
public class ProbationConfiguration : IProbationConfiguration
{
    /// <summary>
    /// Minimum number of game posts to graduate from newbie status.
    /// Default: 100. Changing this requires a DB migration to update the computed column.
    /// </summary>
    public int NewbiePostThreshold { get; set; } = 100;
}
