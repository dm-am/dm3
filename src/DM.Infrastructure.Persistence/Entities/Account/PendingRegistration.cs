using System;
using System.ComponentModel.DataAnnotations;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// Pending registration waiting for email confirmation and username selection.
/// After confirmation, converted to User and deleted.
/// </summary>
public class PendingRegistration
{
    /// <summary>
    /// Primary key
    /// </summary>
    [Key]
    public Guid PendingRegistrationId { get; set; }

    /// <summary>
    /// Token for email activation link (embedded, no FK to Tokens table).
    /// Updated on resend.
    /// </summary>
    public Guid TokenId { get; set; }

    /// <summary>
    /// Email address (unique, lowercase)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Password hash (Argon2id)
    /// </summary>
    [Required]
    [MaxLength(300)]
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Password salt
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string Salt { get; set; } = null!;

    /// <summary>
    /// Password hash algorithm version (4 = Argon2id)
    /// </summary>
    public int PasswordHashVersion { get; set; } = 4;

    /// <summary>
    /// Original registration time (for cleanup after 7 days)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Current token creation time (for 48h expiry check).
    /// Updated on resend.
    /// </summary>
    public DateTimeOffset TokenCreatedUtc { get; set; }

    /// <summary>
    /// Whether user accepted the site rules
    /// </summary>
    public bool AcceptedRules { get; set; }
}
