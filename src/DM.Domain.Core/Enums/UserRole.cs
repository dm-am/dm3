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
    /// System user (Robot Administrator): the author every automated action is
    /// attributed to. There is no automatic ban - warning points are recorded and
    /// read, never compared against a threshold. Not a privilege tier — the role gates are
    /// <c>&gt;=</c> comparisons, so an identity carrying this role would outrank an
    /// administrator in every one of them. The password login refuses it by role,
    /// before the password is compared.
    /// </summary>
    System = 6
}
