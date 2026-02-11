using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Services.DataAccess.BusinessObjects.Users;

/// <summary>
/// DAL model for password history
/// </summary>
[Table("PasswordHistories")]
public class PasswordHistory
{
    /// <summary>
    /// Password history entry identifier
    /// </summary>
    [Key]
    public Guid PasswordHistoryId { get; set; }

    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Password hash
    /// </summary>
    [MaxLength(300)]
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// Password salt
    /// </summary>
    [MaxLength(120)]
    public string Salt { get; set; } = null!;

    /// <summary>
    /// Password hash algorithm version
    /// </summary>
    public int PasswordHashVersion { get; set; }

    /// <summary>
    /// Created moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// User navigation property
    /// </summary>
    public virtual User User { get; set; } = null!;
}
