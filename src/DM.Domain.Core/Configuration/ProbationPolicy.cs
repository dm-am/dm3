namespace DM.Domain.Core.Configuration;

/// <summary>
/// Probation: the single definition of a newbie.
/// </summary>
/// <remarks>
/// A constant and not a setting. The same rule is compiled into a stored computed
/// column on Users, which no value read at runtime can move, so the setting and the
/// schema could not agree: lowering it moved game premoderation, reviews and
/// endorsements while the badge on the profile, the user filter and the assistant
/// lists stayed on the column's hundred. The whole mechanism guarding that was a
/// warning line written at warm-up, after which the site started anyway.
///
/// The number is a product rule, so it moves by a code change and a schema rebuild
/// together, and an architecture test compares this constant with the migration and
/// both model snapshots.
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
