using DM.Domain.Core.Identity;
using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// DTO for pending registration (email confirmation pending)
/// </summary>
public class PendingRegistration
{
    /// <summary>
    /// Primary key
    /// </summary>
    public Guid PendingRegistrationId { get; set; }

    /// <summary>
    /// Token for email activation link
    /// </summary>
    public byte[] SecretHash { get; set; } = null!;

    /// <summary>
    /// The value the activation letter carries. Never stored — the row keeps
    /// <see cref="SecretHash" />.
    /// </summary>
    public Guid Secret { get; set; }

    /// <summary>
    /// Email address (lowercase)
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Password hash (Argon2id)
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Password salt
    /// </summary>
    public string Salt { get; set; } = null!;

    /// <summary>
    /// Password hash algorithm version
    /// </summary>
    public int PasswordHashVersion { get; set; } = PasswordHashing.CurrentVersion;

    /// <summary>
    /// Original registration time
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Current token creation time
    /// </summary>
    public DateTimeOffset TokenCreatedUtc { get; set; }

    /// <summary>
    /// Whether user accepted the site rules
    /// </summary>
    public bool AcceptedRules { get; set; }
}

/// <summary>
/// DTO for creating a new user (during activation)
/// </summary>
public class CreateUser
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// Email address
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Password hash
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Password salt
    /// </summary>
    public string Salt { get; set; } = null!;

    /// <summary>
    /// Password hash algorithm version
    /// </summary>
    public int PasswordHashVersion { get; set; } = PasswordHashing.CurrentVersion;

    /// <summary>
    /// Registration moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// User role
    /// </summary>
    public UserRole Role { get; set; } = UserRole.RegularUser;

    /// <summary>
    /// Access policy
    /// </summary>
    public AccessPolicy AccessPolicy { get; set; } = AccessPolicy.NotSpecified;
}
