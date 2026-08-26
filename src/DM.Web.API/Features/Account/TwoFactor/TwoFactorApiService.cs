using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.TwoFactor;

namespace DM.Web.API.Features.Account.TwoFactor;

/// <inheritdoc />
internal class TwoFactorApiService : ITwoFactorApiService
{
    private readonly ITwoFactorService _twoFactorService;
    private readonly ITwoFactorRemovalService _removalService;

    /// <summary>
    /// Creates a new instance of TwoFactorApiService
    /// </summary>
    public TwoFactorApiService(
        ITwoFactorService twoFactorService,
        ITwoFactorRemovalService removalService)
    {
        _twoFactorService = twoFactorService;
        _removalService = removalService;
    }

    /// <inheritdoc />
    public async Task<TwoFactorStatusResponse> GetStatus()
    {
        var status = await _twoFactorService.GetStatus();
        return new TwoFactorStatusResponse
        {
            Enabled = status.Enabled,
            EnabledUtc = status.EnabledUtc,
            LastVerifiedUtc = status.LastVerifiedUtc,
            RecoveryCodesLeft = status.RecoveryCodesLeft,
            RemovalDueUtc = status.RemovalDueUtc,
            Required = status.Required,
            PrivilegeWithheld = status.PrivilegeWithheld
        };
    }

    /// <inheritdoc />
    public async Task<TwoFactorSetupResponse> Setup(TwoFactorSetupRequest request)
    {
        var setup = await _twoFactorService.IssueSecret(request.Password);
        return new TwoFactorSetupResponse
        {
            Secret = setup.Secret,
            OtpAuthUri = setup.OtpAuthUri
        };
    }

    /// <inheritdoc />
    public async Task<RecoveryCodesResponse> Confirm(TwoFactorConfirmRequest request) =>
        new() { Codes = (await _twoFactorService.Confirm(request.Code)).Codes };

    /// <inheritdoc />
    public Task Disable(TwoFactorConfirmedActionRequest request) =>
        _twoFactorService.Disable(request.Password, request.Code);

    /// <inheritdoc />
    public async Task<RecoveryCodesResponse> ReissueRecoveryCodes(
        TwoFactorConfirmedActionRequest request) =>
        new()
        {
            Codes = (await _twoFactorService.ReissueRecoveryCodes(request.Password, request.Code)).Codes
        };

    /// <inheritdoc />
    public Task RequestRemoval(TwoFactorRemovalRequest request) =>
        _removalService.Request(request.Email);

    /// <inheritdoc />
    public Task ScheduleRemoval(Guid secret) => _removalService.Schedule(secret);

    /// <inheritdoc />
    public Task CancelRemoval(Guid secret) => _removalService.Cancel(secret);

    /// <inheritdoc />
    public Task ClearForColleague(string username) => _removalService.ClearForColleague(username);
}
