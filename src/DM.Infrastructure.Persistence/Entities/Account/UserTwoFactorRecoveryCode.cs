using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for one recovery code: a fixed number of rows per account.
/// </summary>
/// <remarks>
/// A spent code is stamped rather than deleted. Deleting it would erase the
/// trail, and the stamp is what answers both "somebody used one of my paper
/// codes" in the journal and "how many are left" on the settings screen.
/// </remarks>
[Table("UserTwoFactorRecoveryCodes")]
public class UserTwoFactorRecoveryCode
{
    /// <summary>
    /// Row identifier
    /// </summary>
    public Guid RecoveryCodeId { get; set; }

    /// <summary>
    /// Owner
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// SHA-256 of the code. The value itself exists in one answer and in one
    /// request, and nowhere else
    /// </summary>
    public byte[] CodeHash { get; set; } = null!;

    /// <summary>
    /// When the set this code belongs to was issued
    /// </summary>
    public DateTimeOffset IssuedUtc { get; set; }

    /// <summary>
    /// When it was spent. NULL means unspent
    /// </summary>
    public DateTimeOffset? UsedUtc { get; set; }

    /// <summary>
    /// Address it was spent from
    /// </summary>
    public string? UsedFromIp { get; set; }
}
