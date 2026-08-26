using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// Storage of the second factor: its state, its recovery codes and the
/// challenges standing between a proven password and a session.
/// </summary>
public interface ITwoFactorRepository
{
    /// <summary>
    /// Whether the account has a confirmed factor.
    /// </summary>
    /// <remarks>
    /// The narrowest read there is, and the only one on the hot path: the fold
    /// that withholds a rank asks this and nothing else, so the secret never
    /// travels with an ordinary request.
    /// </remarks>
    /// <param name="userId">Account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> IsConfirmed(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The factor of one account, secret included.
    /// </summary>
    /// <param name="userId">Account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TwoFactorState?> Find(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The account the removal paths address, by its confirmed address.
    /// </summary>
    /// <param name="email">Address</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TwoFactorAccount?> FindAccountByEmail(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// The account the removal paths address, by name.
    /// </summary>
    /// <param name="username">Name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TwoFactorAccount?> FindAccountByUsername(
        string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// The account the removal paths address, by identifier.
    /// </summary>
    /// <param name="userId">Account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TwoFactorAccount?> FindAccount(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace whatever unconfirmed factor the account has with a new secret.
    /// </summary>
    /// <remarks>
    /// Replace rather than return the old one: somebody who abandoned a QR on
    /// one device and started again on another must not be left with two live
    /// secrets, and the abandoned one may have been read over a shoulder.
    /// </remarks>
    /// <param name="userId">Account</param>
    /// <param name="encryptedSecret">Secret in its AEAD envelope</param>
    /// <param name="createdUtc">Moment the secret is issued at</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task IssueSecret(
        Guid userId, string encryptedSecret, DateTimeOffset createdUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Switch the factor on and issue the recovery set that comes with it, in
    /// one transaction.
    /// </summary>
    /// <remarks>
    /// One call and not two, because the two halves are one promise: the set is
    /// shown exactly once, in the answer to the confirmation, and a factor
    /// switched on beside a failure to store the set leaves the owner locked to
    /// a device with no way past it. Either both, or the factor stays off and
    /// the person confirms again.
    ///
    /// Conditional on the factor still being off, so two confirmations racing
    /// each other cannot both report having switched it on. The time step of
    /// the confirming code is already recorded by the verification that
    /// preceded this call.
    /// </remarks>
    /// <param name="userId">Account</param>
    /// <param name="confirmedUtc">Moment of confirmation, and of the set's issue</param>
    /// <param name="recoveryCodeHashes">Hashes of the set issued with the confirmation</param>
    /// <returns>Whether the factor was off and is now on</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> Confirm(
        Guid userId, DateTimeOffset confirmedUtc, IReadOnlyList<byte[]> recoveryCodeHashes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record an accepted time step, and only if it is later than the recorded
    /// one.
    /// </summary>
    /// <remarks>
    /// Conditional on purpose: this is the replay guard, and two requests
    /// carrying one code must not both win. Zero rows updated means the step
    /// was already spent.
    /// </remarks>
    /// <param name="userId">Account</param>
    /// <param name="step">Step the code was computed for</param>
    /// <param name="verifiedUtc">Moment of the verification</param>
    /// <returns>Whether this caller was the one that claimed the step</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> TryAcceptStep(
        Guid userId, long step, DateTimeOffset verifiedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stamp a successful verification that did not come from a time step.
    /// </summary>
    /// <param name="userId">Account</param>
    /// <param name="verifiedUtc">Moment of the verification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkVerified(Guid userId, DateTimeOffset verifiedUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove the factor and every recovery code with it.
    /// </summary>
    /// <param name="userId">Account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Remove(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replace the whole set of recovery codes.
    /// </summary>
    /// <remarks>
    /// The previous set goes as a whole: half old and half new means a code
    /// crossed off on paper still opens the account.
    /// </remarks>
    /// <param name="userId">Account</param>
    /// <param name="codeHashes">Hashes of the new set</param>
    /// <param name="issuedUtc">Moment the set is issued at</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReplaceRecoveryCodes(
        Guid userId, IReadOnlyList<byte[]> codeHashes, DateTimeOffset issuedUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The unspent codes of an account, as identifier and hash.
    /// </summary>
    /// <remarks>
    /// Read rather than matched in the query on purpose: the comparison of a
    /// presented value against a stored one is required to take the same time
    /// whether it matches or not, and that is a property of a comparison written
    /// here, not of a predicate handed to the database.
    /// </remarks>
    /// <param name="userId">Account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IReadOnlyList<(Guid RecoveryCodeId, byte[] CodeHash)>> GetUnusedRecoveryCodes(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Spend one recovery code, and only if it is unspent.
    /// </summary>
    /// <remarks>
    /// A conditional update touching exactly one row. Two requests carrying one
    /// code reach it together, and only one of them changes anything; zero rows
    /// changed is a refusal.
    /// </remarks>
    /// <param name="recoveryCodeId">The code that matched</param>
    /// <param name="usedUtc">Moment it is spent at</param>
    /// <param name="usedFromIp">Address it is spent from</param>
    /// <returns>Whether this caller was the one that spent it</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> TrySpendRecoveryCode(
        Guid recoveryCodeId, DateTimeOffset usedUtc, string? usedFromIp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// How many codes of the current set are unspent.
    /// </summary>
    /// <param name="userId">Account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> CountUnusedRecoveryCodes(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a challenge.
    /// </summary>
    /// <param name="challenge">Challenge to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddChallenge(TwoFactorChallengeState challenge, CancellationToken cancellationToken = default);

    /// <summary>
    /// The challenge by its identifier, whatever state it is in.
    /// </summary>
    /// <param name="challengeId">Challenge identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TwoFactorChallengeState?> FindChallenge(
        Guid challengeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count one failed attempt against the challenge.
    /// </summary>
    /// <param name="challengeId">Challenge identifier</param>
    /// <returns>How many attempts the challenge has now spent</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> CountChallengeAttempt(Guid challengeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete one challenge.
    /// </summary>
    /// <param name="challengeId">Challenge identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveChallenge(Guid challengeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete every challenge of an account.
    /// </summary>
    /// <remarks>
    /// Called when the owner changes the password by any route: a change of
    /// password is a statement that the account may be compromised, and a login
    /// begun with the old one must not survive it.
    /// </remarks>
    /// <param name="userId">Account</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RemoveChallengesOf(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Schedule the mailed removal of the factor.
    /// </summary>
    /// <param name="userId">Account</param>
    /// <param name="dueUtc">Moment the removal takes effect</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ScheduleRemoval(Guid userId, DateTimeOffset dueUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Call off a scheduled removal.
    /// </summary>
    /// <param name="userId">Account</param>
    /// <returns>Whether a removal was pending</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> CancelScheduledRemoval(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accounts whose scheduled removal has come due.
    /// </summary>
    /// <param name="now">Moment the pass runs at</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IReadOnlyList<Guid>> FindRemovalsDue(
        DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete factors that were issued and never confirmed within the window.
    /// </summary>
    /// <param name="issuedBefore">Cutoff the setup window puts on issue time</param>
    /// <returns>How many were deleted</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> DeleteAbandonedSetups(
        DateTimeOffset issuedBefore, CancellationToken cancellationToken = default);
}
