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
    public int PasswordHashVersion { get; set; } = 4;

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
