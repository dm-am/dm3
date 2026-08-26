using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Identity;

/// <summary>
/// DTO model for authenticated user
/// </summary>
public class AuthenticatedUser : GeneralUser, IAuthorizationSubject
{
    /// <summary>
    /// Password salt
    /// </summary>
    public string Salt { get; set; } = null!;

    /// <summary>
    /// Password hash
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Password hash algorithm version (4 = Argon2id)
    /// </summary>
    public int PasswordHashVersion { get; set; } = PasswordHashing.CurrentVersion;

    /// <summary>
    /// Removed flag
    /// </summary>
    public bool IsRemoved { get; set; }

    /// <summary>
    /// Restrictions from the user's bans, each with the window it is in force for
    /// </summary>
    public IEnumerable<AccessRestriction> AccessRestrictions { get; set; } = [];

    /// <summary>
    /// The policy that actually applies at the given moment: the user's own
    /// restriction plus every ban in force right now.
    /// </summary>
    /// <remarks>
    /// This must be folded into <see cref="GeneralUser.AccessPolicy" /> once, when
    /// the identity is built, because that is the property every authorization
    /// resolver reads and resolvers have no clock. Bans live in their own table
    /// and nothing ever writes the user's own column, so reading the column
    /// without this fold means no ban has any effect whatsoever.
    /// </remarks>
    /// <param name="moment">Current moment</param>
    public AccessPolicy EffectiveAccessPolicyAt(DateTimeOffset moment) =>
        AccessRestrictions
            .Where(restriction => restriction.IsInForceAt(moment))
            .Aggregate(AccessPolicy, (seed, restriction) => seed | restriction.Policy);

    private UserRole? recordedRole;

    /// <summary>
    /// The role as it stands on the account, whatever role the account is acting
    /// with right now.
    /// </summary>
    /// <remarks>
    /// Display and the journals read this one. A moderator recorded as an
    /// ordinary user is an entry nothing can be reconstructed from afterwards,
    /// and the demotion below is about what may be done, not about who this is.
    ///
    /// Equal to <see cref="GeneralUser.Role" /> until the fold runs, so a user
    /// built by a path that never folds still answers honestly.
    /// </remarks>
    public UserRole RecordedRole => recordedRole ?? Role;

    /// <summary>
    /// Whether the rank's privileges are withheld for want of a second factor.
    /// </summary>
    public bool PrivilegeWithheld { get; private set; }

    /// <summary>
    /// Withhold the privileges of a rank that owes a second factor it has not
    /// set up.
    /// </summary>
    /// <remarks>
    /// Folded into <see cref="GeneralUser.Role" /> once, at the point the
    /// identity is built, exactly as active bans are folded into
    /// <see cref="GeneralUser.AccessPolicy" />: the role is what every resolver
    /// and every hand-written rank comparison in the moderation services reads,
    /// and a surface obliged to remember a second flag is a surface that will
    /// one day forget it.
    ///
    /// Never a refusal to authenticate. The account signs in, reads the site and
    /// writes its own; what it loses is the rank, and the only person who can
    /// give it back is its owner, from an ordinary session, in the time it takes
    /// to scan a code.
    /// </remarks>
    /// <param name="secondFactorConfirmed">Whether the account has a confirmed factor.</param>
    public void ApplySecondFactorRequirement(bool secondFactorConfirmed)
    {
        recordedRole = Role;
        PrivilegeWithheld = !secondFactorConfirmed && TwoFactorRequirement.AppliesTo(Role);
        if (PrivilegeWithheld)
        {
            Role = TwoFactorRequirement.WithheldTo;
        }
    }

    /// <summary>
    /// Basic guest user (unauthenticated)
    /// </summary>
    /// <remarks>
    /// A fresh instance per call, not a shared one: identities are mutated after
    /// construction when active bans are folded in, and a single object handed to
    /// every anonymous request in the process would carry one request's mutation
    /// into all the others.
    /// </remarks>
    public static AuthenticatedUser Guest => new();
}
