using System.Collections.Generic;
using System.Linq;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Authentication.Dto;

/// <summary>
/// DTO model for authenticated user
/// </summary>
public class AuthenticatedUser : GeneralUser
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
    /// Password hash algorithm version (1 = SHA256, 2 = PBKDF2)
    /// </summary>
    public int PasswordHashVersion { get; set; } = 1;

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