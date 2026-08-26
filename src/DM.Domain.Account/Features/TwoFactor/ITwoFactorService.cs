using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.TwoFactor;

/// <summary>
/// The second factor as its owner manages it, from a session he already has.
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// What the account area says about the factor.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TwoFactorStatus> GetStatus(CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue a secret and hand it over, once.
    /// </summary>
    /// <remarks>
    /// The password is asked for again and this is not a formality. A session
    /// lives a year, and a stolen one would otherwise be enough to put an
    /// attacker's own factor on somebody else's account - after which the owner
    /// meets a code he does not have.
    ///
    /// Called again before confirmation, it replaces the secret rather than
    /// handing back the previous one: there is one live unconfirmed secret at a
    /// time, and it is always the last one issued.
    /// </remarks>
    /// <param name="currentPassword">The account's current password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<TwoFactorSetup> IssueSecret(string currentPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirm the issued secret with the first code from the device and switch
    /// the factor on.
    /// </summary>
    /// <remarks>
    /// The only path that switches the factor on. Everything that can go wrong
    /// with a setup - a clock adrift, an app that saved nothing, a smeared QR,
    /// the wrong code scanned - is discovered here rather than days later at a
    /// sign-in where the only way in left is recovery.
    /// </remarks>
    /// <param name="code">First code from the device</param>
    /// <returns>The recovery codes, shown once</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<RecoveryCodeSet> Confirm(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switch the factor off.
    /// </summary>
    /// <remarks>
    /// Costs the password and a passed second factor, because switching off is a
    /// change of the same weight as switching on. A recovery code counts as the
    /// second factor here, or somebody who lost the device could never take the
    /// factor off at all.
    /// </remarks>
    /// <param name="currentPassword">The account's current password</param>
    /// <param name="secondFactor">Code from the device, or a recovery code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task Disable(
        string currentPassword, string secondFactor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issue a fresh set of recovery codes, retiring the previous set whole.
    /// </summary>
    /// <param name="currentPassword">The account's current password</param>
    /// <param name="secondFactor">Code from the device, or a recovery code</param>
    /// <returns>The new codes, shown once</returns>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<RecoveryCodeSet> ReissueRecoveryCodes(
        string currentPassword, string secondFactor, CancellationToken cancellationToken = default);
}
