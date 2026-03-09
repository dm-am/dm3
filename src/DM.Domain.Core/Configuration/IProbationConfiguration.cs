namespace DM.Domain.Core.Configuration;

/// <summary>
/// Configuration for user probation (newbie) thresholds
/// </summary>
public interface IProbationConfiguration
{
    /// <summary>
    /// Minimum number of game posts to graduate from newbie status.
    /// Default: 100. Changing this requires a DB migration to update the computed column.
    /// </summary>
    int NewbiePostThreshold { get; }
}
