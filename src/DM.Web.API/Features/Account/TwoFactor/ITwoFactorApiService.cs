using System.Threading.Tasks;

namespace DM.Web.API.Features.Account.TwoFactor;

/// <summary>
/// API service for the second factor of the caller's own account
/// </summary>
public interface ITwoFactorApiService
{
    /// <summary>
    /// State of the factor
    /// </summary>
    /// <returns>What the settings screen shows</returns>
    Task<TwoFactorStatusResponse> GetStatus();

    /// <summary>
    /// Issue a secret and hand it over, once
    /// </summary>
    /// <param name="request">Current password</param>
    /// <returns>Secret and otpauth URI</returns>
    Task<TwoFactorSetupResponse> Setup(TwoFactorSetupRequest request);

    /// <summary>
    /// Confirm the secret and switch the factor on
    /// </summary>
    /// <param name="request">First code from the device</param>
    /// <returns>The recovery codes, shown once</returns>
    Task<RecoveryCodesResponse> Confirm(TwoFactorConfirmRequest request);

    /// <summary>
    /// Switch the factor off
    /// </summary>
    /// <param name="request">Current password and a passed second factor</param>
    Task Disable(TwoFactorConfirmedActionRequest request);

    /// <summary>
    /// Reissue the recovery codes, retiring the previous set whole
    /// </summary>
    /// <param name="request">Current password and a passed second factor</param>
    /// <returns>The new codes, shown once</returns>
    Task<RecoveryCodesResponse> ReissueRecoveryCodes(TwoFactorConfirmedActionRequest request);

    /// <summary>
    /// Ask, from the mailbox, for the factor to be taken off
    /// </summary>
    /// <param name="request">Address the account answers at</param>
    Task RequestRemoval(TwoFactorRemovalRequest request);

    /// <summary>
    /// Follow the link from the letter: schedule the removal
    /// </summary>
    /// <param name="secret">Value from the link</param>
    Task ScheduleRemoval(System.Guid secret);

    /// <summary>
    /// Follow the second link: call the scheduled removal off
    /// </summary>
    /// <param name="secret">Value from the link</param>
    Task CancelRemoval(System.Guid secret);

    /// <summary>
    /// Take a colleague's factor off, as the second administrator
    /// </summary>
    /// <param name="username">Whose factor to take off</param>
    Task ClearForColleague(string username);
}
