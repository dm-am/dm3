using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Authorization;

/// <summary>
/// Whom a rank reaches: the one rule every moderation action taken against a
/// person obeys - a warning, a ban, the removal of an account.
/// </summary>
/// <remarks>
/// Strictly below your own role, so equal ranks cannot reach each other; never an
/// administrator or System: there is no in-app authority over the site owner.
/// One formula for the service and the screen that offers the action.
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
