namespace DM.Domain.Core.Configuration;

/// <summary>
/// Probation: the single definition of a newbie.
/// </summary>
/// <remarks>
/// A constant and not a setting: the same rule is a stored computed column on Users,
/// and an architecture test compares it with the migration and both model snapshots.
/// </remarks>
public static class ProbationPolicy
{
    /// <summary>
    /// Game posts a user needs before they stop being a newbie.
    /// </summary>
    public const int NewbiePostThreshold = 100;

    /// <summary>
    /// Whether that many game posts still belong to a newbie.
    /// </summary>
    public static bool IsNewbie(int gamePostCount) => gamePostCount < NewbiePostThreshold;
}
