namespace DM.Domain.Core.Enums;

/// <summary>
/// Source of subscription creation
/// </summary>
public enum SubscriptionSource
{
    /// <summary>
    /// Manually created by user
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Automatically created from participation (e.g., joining a game)
    /// </summary>
    Participation = 1
}
