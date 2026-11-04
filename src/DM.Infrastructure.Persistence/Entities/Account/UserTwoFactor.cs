using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Account;

/// <summary>
/// DAL model for the second factor: one row per account, keyed by the account.
/// </summary>
/// <remarks>
/// Its own table so the secret stays out of the user row read on every request.
/// The secret is encrypted, not hashed, because verifying a code needs the secret itself.
/// </remarks>
[Table("UserTwoFactors")]
public class UserTwoFactor
{
    /// <summary>
    /// Owner, and the key: one factor per account
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The shared secret in the AEAD envelope, which carries its key version
    /// </summary>
    public string Secret { get; set; } = null!;

    /// <summary>
    /// When the secret was issued. The setup window is counted from here
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// When the factor was switched on. NULL means it is not on, and there is
    /// deliberately no boolean beside it
    /// </summary>
    public DateTimeOffset? ConfirmedUtc { get; set; }

    /// <summary>
    /// Last successful verification, by a code or by a recovery code
    /// </summary>
    public DateTimeOffset? LastVerifiedUtc { get; set; }

    /// <summary>
    /// The last accepted time step, and the whole of the replay guard
    /// </summary>
    public long LastAcceptedStep { get; set; }

    /// <summary>
    /// When the set of recovery codes in force was issued
    /// </summary>
    public DateTimeOffset? RecoveryCodesIssuedUtc { get; set; }

    /// <summary>
    /// When a removal scheduled from the mailbox takes effect. NULL means none
    /// is pending
    /// </summary>
    public DateTimeOffset? RemovalDueUtc { get; set; }
}
