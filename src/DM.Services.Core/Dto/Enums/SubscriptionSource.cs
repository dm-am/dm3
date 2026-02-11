namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Source of subscription creation
/// </summary>
public enum SubscriptionSource
{
    /// <summary>
    /// Created manually by user
    /// </summary>
    Manual = 0,

    /// <summary>
    /// Created automatically from participation (reader, player, etc.)
    /// </summary>
    Participation = 1
}
