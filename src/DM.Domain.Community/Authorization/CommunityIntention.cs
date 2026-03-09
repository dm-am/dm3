namespace DM.Domain.Community.Authorization;

/// <summary>
/// List of community-wide actions that require authorization
/// </summary>
public enum CommunityIntention
{
    /// <summary>
    /// View list of users pending activation.
    /// Requires: SeniorModerator+
    /// </summary>
    ViewPendingUsers = 1
}
