namespace DM.Domain.Core.Enums;

/// <summary>
/// Filter for user listing by activity status
/// </summary>
public enum UserActivityFilter
{
    /// <summary>
    /// Users active within <see cref="Configuration.ActivityPolicy.ActivePeriod" /> (default)
    /// </summary>
    Active = 0,

    /// <summary>
    /// All activated users regardless of activity
    /// </summary>
    All = 1,

    /// <summary>
    /// Users not active within <see cref="Configuration.ActivityPolicy.ActivePeriod" />
    /// </summary>
    Inactive = 3
}
