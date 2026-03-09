namespace DM.Domain.Core.Enums;

/// <summary>
/// Filter for user listing by activity status
/// </summary>
public enum UserActivityFilter
{
    /// <summary>
    /// Users active within the last month (default)
    /// </summary>
    Active = 0,

    /// <summary>
    /// All activated users regardless of activity
    /// </summary>
    All = 1,

    /// <summary>
    /// Only users pending activation (requires SeniorModerator+)
    /// </summary>
    Pending = 2
}
