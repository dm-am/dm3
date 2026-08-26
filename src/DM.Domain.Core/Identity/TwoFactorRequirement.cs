using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Identity;

/// <summary>
/// Which ranks may not exercise their privileges without a second factor.
/// </summary>
/// <remarks>
/// The single definition, and it has to be single for the same reason
/// <see cref="AccessRestriction.IsInForceAt" /> is: the predicate is folded into
/// the identity once, at the point where it is built, and every authorization
/// surface reads the folded role rather than asking this question again. A
/// second spelling anywhere means one surface keeps handing out a privilege the
/// other withholds.
///
/// The two named ranks and not "everything above the moderator": the plan names
/// these two, and every rank added widens the set of accounts that can find
/// themselves demoted. System is deliberately outside it - the actor never signs
/// in, so a requirement on it would guard nothing and would demote the author of
/// every automatic action.
/// </remarks>
public static class TwoFactorRequirement
{
    /// <summary>
    /// Whether an account of this rank owes a second factor.
    /// </summary>
    /// <param name="role">Role as recorded on the account.</param>
    public static bool AppliesTo(UserRole role) =>
        role is UserRole.Admin or UserRole.SeniorModerator;

    /// <summary>
    /// The rank an account of this role acts with while it owes a factor it has
    /// not set up.
    /// </summary>
    /// <remarks>
    /// <see cref="UserRole.RegularUser" /> and not <see cref="UserRole.Moderator" />:
    /// leaving a compromised administrator the moderator's edit and delete of
    /// other people's content leaves most of the damage the factor is bought
    /// against. The demotion is not a punishment - it lasts as long as scanning
    /// a QR code takes.
    /// </remarks>
    public const UserRole WithheldTo = UserRole.RegularUser;
}
