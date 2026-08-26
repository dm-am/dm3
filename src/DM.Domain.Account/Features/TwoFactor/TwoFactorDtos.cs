using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// State of the second factor of one account, as the storage holds it.
/// </summary>
/// <remarks>
/// <see cref="ConfirmedUtc" /> is the whole definition of "the factor is on".
/// There is deliberately no boolean beside it: a flag and a moment are two
/// sources for one truth, and one day they disagree.
/// </remarks>
public class TwoFactorState
{
    /// <summary>Owner of the factor</summary>
    public Guid UserId { get; set; }

    /// <summary>Secret in the AEAD envelope, exactly as it is stored</summary>
    public string Secret { get; set; } = null!;

    /// <summary>When the secret was issued</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>When the factor was switched on; null means it is not on</summary>
    public DateTimeOffset? ConfirmedUtc { get; set; }

    /// <summary>Last successful verification, by code or by recovery code</summary>
    public DateTimeOffset? LastVerifiedUtc { get; set; }

    /// <summary>Last accepted time step; the basis of the replay guard</summary>
    public long LastAcceptedStep { get; set; }

    /// <summary>When the set of recovery codes in force was issued</summary>
    public DateTimeOffset? RecoveryCodesIssuedUtc { get; set; }

    /// <summary>When a mailed removal takes effect; null means none is pending</summary>
    public DateTimeOffset? RemovalDueUtc { get; set; }

    /// <summary>Whether the factor is on</summary>
    public bool IsConfirmed => ConfirmedUtc.HasValue;
}

/// <summary>
/// What the owner is shown once, at the moment the secret is issued.
/// </summary>
/// <param name="Secret">Base32 secret, for typing in by hand</param>
/// <param name="OtpAuthUri">The same secret as an otpauth URI, for the QR code</param>
public record TwoFactorSetup(string Secret, string OtpAuthUri);

/// <summary>
/// What the account area says about the factor without disclosing anything that
/// could be used to pass it.
/// </summary>
/// <param name="Enabled">Whether the factor is on</param>
/// <param name="EnabledUtc">Since when</param>
/// <param name="LastVerifiedUtc">Last time it was passed</param>
/// <param name="RecoveryCodesLeft">How many recovery codes are unspent</param>
/// <param name="RemovalDueUtc">When a scheduled removal takes effect, if one is pending</param>
/// <param name="Required">Whether this account's rank owes a factor</param>
/// <param name="PrivilegeWithheld">Whether the rank is withheld for want of one</param>
public record TwoFactorStatus(
    bool Enabled,
    DateTimeOffset? EnabledUtc,
    DateTimeOffset? LastVerifiedUtc,
    int RecoveryCodesLeft,
    DateTimeOffset? RemovalDueUtc,
    bool Required,
    bool PrivilegeWithheld);

/// <summary>
/// A set of recovery codes at the one moment it exists in the clear.
/// </summary>
/// <param name="Codes">The values, shown once and never again</param>
public record RecoveryCodeSet(IReadOnlyList<string> Codes);

/// <summary>
/// The challenge that stands between a proven password and a session.
/// </summary>
public class TwoFactorChallengeState
{
    /// <summary>Challenge identifier, the value the cookie carries</summary>
    public Guid ChallengeId { get; set; }

    /// <summary>Whose login is unfinished</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The account identifier the password step counted attempts under.
    /// </summary>
    /// <remarks>
    /// Kept so that a wrong code lands in the same counter as a wrong password
    /// rather than in one of its own: two counters over one login is two
    /// thresholds, and the lower of them is the only one that matters.
    /// </remarks>
    public string Account { get; set; } = null!;

    /// <summary>When the challenge was created</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>When it stops being usable</summary>
    public DateTimeOffset ExpiresUtc { get; set; }

    /// <summary>"Remember me" as it was ticked on the first step</summary>
    public bool Persistent { get; set; }

    /// <summary>How many codes have already failed</summary>
    public int Attempts { get; set; }

    /// <summary>Address the first step came from</summary>
    public string? IpAddress { get; set; }

    /// <summary>User agent the first step came from</summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// The little the removal paths need to know about an account.
/// </summary>
/// <remarks>
/// Not the authenticated user: nothing here decides an authentication, so the
/// password columns have no business travelling. The role is on it because the
/// mailed path is closed for the ranks that owe a factor, and the address
/// because that is where the two letters go.
/// </remarks>
/// <param name="UserId">Account</param>
/// <param name="Username">Name, for the letters</param>
/// <param name="Email">Confirmed address</param>
/// <param name="Role">Role as recorded on the account</param>
public record TwoFactorAccount(
    Guid UserId, string Username, string? Email, UserRole Role);
