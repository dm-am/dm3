namespace DM.Domain.Account.Configuration;

/// <summary>
/// Numbers the second factor is governed by.
/// </summary>
/// <remarks>
/// Every one of them is a product decision rather than an implementation
/// detail: how long a person has to install an authenticator, how long a login
/// left half-finished stays worth stealing, how many wrong codes one login is
/// allowed, and how long a mailbox has to wait before it can take the factor
/// off an account.
/// </remarks>
public class TwoFactorConfiguration
{
    /// <summary>
    /// Name of the issuer written into the otpauth URI and shown in the
    /// authenticator app.
    /// </summary>
    /// <remarks>
    /// Configuration and not the request's host: the site answers on several
    /// addresses, and the entry an app files away must not depend on which one
    /// the person happened to be reading.
    /// </remarks>
    public string Issuer { get; set; } = "Dungeon Master";

    /// <summary>
    /// How long an issued but unconfirmed secret stays usable, in minutes.
    /// </summary>
    /// <remarks>
    /// A compromise. Less refuses the person who is honestly installing an app
    /// for the first time; more leaves a credential in the table belonging to
    /// somebody who has forgotten about it.
    /// </remarks>
    public int SetupWindowMinutes { get; set; } = 30;

    /// <summary>
    /// How long a challenge between the two factors lives, in minutes.
    /// </summary>
    public int ChallengeLifetimeMinutes { get; set; } = 5;

    /// <summary>
    /// How many wrong codes one challenge survives.
    /// </summary>
    /// <remarks>
    /// Counted on the challenge and not on the account on purpose: an account
    /// counter would let anyone who knows the password freeze somebody else's
    /// second factor. The account-and-address counter that already guards the
    /// password is the second level and is not restated here.
    /// </remarks>
    public int ChallengeAttemptLimit { get; set; } = 5;

    /// <summary>
    /// How many time steps either side of the current one are accepted.
    /// </summary>
    public int VerificationWindowSteps { get; set; } = 1;

    /// <summary>
    /// How many recovery codes one set holds.
    /// </summary>
    public int RecoveryCodeCount { get; set; } = 10;

    /// <summary>
    /// How long the mailed removal of the factor waits before it takes effect,
    /// in days.
    /// </summary>
    /// <remarks>
    /// The whole point of the mailed path is this delay: whoever holds the
    /// mailbox and the password gets the account either way, but only after a
    /// week of noise the owner can cancel with one successful sign-in.
    /// </remarks>
    public int RemovalDelayDays { get; set; } = 7;
}
