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

    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Password hash algorithm version
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
    /// Folded into <see cref="GeneralUser.AccessPolicy" /> when the identity is
    /// built, because resolvers read that property and have no clock.
    /// </remarks>
    public AccessPolicy EffectiveAccessPolicyAt(DateTimeOffset moment) =>
        AccessRestrictions
            .Where(restriction => restriction.IsInForceAt(moment))
            .Aggregate(AccessPolicy, (seed, restriction) => seed | restriction.Policy);

    /// <summary>
    /// When the full ban in force at the moment runs out, or null when there is
    /// none or when the one there is runs past the permanence threshold.
    /// </summary>
    /// <remarks>
    /// The screen spells the day itself, in the reader's own time zone and in
    /// its own words, so what travels is the moment and not a sentence. A ban
    /// fifty years out is told as "indefinitely": a date that far reads as a
    /// lie dressed as precision. Computed here, on the user the ban was read
    /// for, and not on the identity of a refused sign-in, which carries the
    /// guest and no restrictions at all.
    /// </remarks>
    public DateTimeOffset? BanEndsAt(DateTimeOffset moment)
    {
        var end = AccessRestrictions
            .Where(restriction => restriction.IsInForceAt(moment) &&
                                  restriction.Policy.HasFlag(AccessPolicy.FullBan))
            .Select(restriction => (DateTimeOffset?)restriction.EndedUtc)
            .Max();
        return end is { } ends && ends <= moment.AddYears(PermanenceThresholdYears) ? ends : null;
    }

    // A ban that runs longer than this is told as one with no end
    private const int PermanenceThresholdYears = 50;

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
