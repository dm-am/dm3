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
    /// Ban access restriction policies
    /// </summary>
    public IEnumerable<AccessPolicy> AccessRestrictionPolicies { get; set; } = [];

    /// <summary>
    /// Calculated restriction policy based on ban and personal restrictions
    /// </summary>
    public AccessPolicy GeneralAccessPolicy =>
        AccessRestrictionPolicies.Aggregate(AccessPolicy, (seed, restriction) => seed | restriction);

    /// <summary>
    /// Basic guest user (unauthenticated)
    /// </summary>
    public static readonly AuthenticatedUser Guest = new();
}
