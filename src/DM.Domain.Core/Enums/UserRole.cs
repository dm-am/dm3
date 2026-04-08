namespace DM.Domain.Core.Enums;

/// <summary>
/// User role on the website (hierarchical, not flags)
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Unauthenticated user
    /// </summary>
    Guest = 0,

    /// <summary>
    /// Regular authenticated user
    /// </summary>
    RegularUser = 1,

    /// <summary>
    /// Experienced user who helps newbies, can release games from premoderation
    /// </summary>
    Mentor = 2,

    /// <summary>
    /// Junior moderator, can give warnings, release from premoderation
    /// </summary>
    Moderator = 3,

    /// <summary>
    /// Senior moderator, can ban users, create polls, override other moderators
    /// </summary>
    SeniorModerator = 4,

    /// <summary>
    /// Administrator with full access
    /// </summary>
    Admin = 5,

    /// <summary>
    /// System user (Robot Administrator).
    /// Cannot login, used for automated actions like auto-bans.
    /// </summary>
    System = 6
}
