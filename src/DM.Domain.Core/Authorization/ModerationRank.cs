using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Authorization;

/// <summary>
/// Whom a rank reaches: the one rule every moderation action taken against a
/// person obeys - a warning, a ban, the removal of an account.
/// </summary>
/// <remarks>
/// Strictly below your own role, and never an administrator. Equal ranks do not
/// reach each other: two senior moderators would otherwise ban each other, and a
/// moderator would warn a colleague of his own rank. A warning from below is
/// worth nothing anyway: the senior moderator it was given to may clear its
/// points himself. Nobody reaches an administrator, the site owner: there is
/// deliberately no in-app authority over one, and control over an owner lives
/// outside the application. The System role stands above Admin in the
/// enumeration and is reached by nobody for the same comparison.
///
/// One formula for the service that refuses the action and for the screen that
/// offers it, so a button is never drawn for a person the write then refuses.
/// </remarks>
public static class ModerationRank
{
    /// <summary>
    /// Whether an action of <paramref name="actor"/>'s rank reaches a person of
    /// <paramref name="target"/>'s rank.
    /// </summary>
    public static bool Reaches(UserRole actor, UserRole target) =>
        target < actor && target < UserRole.Admin;
}
