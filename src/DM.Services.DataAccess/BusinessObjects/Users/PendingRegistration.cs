using System;
using System.ComponentModel.DataAnnotations;

namespace DM.Services.DataAccess.BusinessObjects.Users;

/// <summary>
/// Pending registration waiting for email confirmation and login selection.
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
    /// Password hash (PBKDF2-SHA256)
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
    /// Password hash algorithm version (for future upgrades)
    /// </summary>
    public int PasswordHashVersion { get; set; }

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
