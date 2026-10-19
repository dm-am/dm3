using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.TwoFactor;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Features.Account.TwoFactor;

public class TwoFactorApiService
{
    private readonly ITwoFactorService _twoFactorService;
    private readonly ITwoFactorRemovalService _removalService;
    private readonly TokenConfiguration _tokens;

    public TwoFactorApiService(
        ITwoFactorService twoFactorService,
        ITwoFactorRemovalService removalService,
        IOptions<TokenConfiguration> tokens)
    {
        _twoFactorService = twoFactorService;
        _removalService = removalService;
        _tokens = tokens.Value;
    }

    public async Task<TwoFactorStatusResponse> GetStatus()
    {
        var status = await _twoFactorService.GetStatus();
        return new TwoFactorStatusResponse
        {
            Enabled = status.Enabled,
            EnabledUtc = status.EnabledUtc,
            LastVerifiedUtc = status.LastVerifiedUtc,
            RecoveryCodesLeft = status.RecoveryCodesLeft,
            RemovalDueUtc = status.RemovalDueUtc
        };
    }

    public async Task<TwoFactorSetupResponse> Setup(TwoFactorSetupRequest request)
    {
        var setup = await _twoFactorService.IssueSecret(request.Password);
        return new TwoFactorSetupResponse
        {
            Secret = setup.Secret,
            OtpAuthUri = setup.OtpAuthUri
        };
    }

    public async Task<RecoveryCodesResponse> Confirm(TwoFactorConfirmRequest request) =>
        new() { Codes = (await _twoFactorService.Confirm(request.Code)).Codes };

    public Task Disable(TwoFactorConfirmedActionRequest request) =>
        _twoFactorService.Disable(request.Password, request.Code);

    public async Task<RecoveryCodesResponse> ReissueRecoveryCodes(
        TwoFactorConfirmedActionRequest request) =>
        new()
        {
            Codes = (await _twoFactorService.ReissueRecoveryCodes(request.Password, request.Code)).Codes
        };

    /// <returns>The lifetime of the link, the same for every address: the answer must not tell them apart</returns>
    public async Task<MailedLink> RequestRemoval(TwoFactorRemovalRequest request)
    {
        await _removalService.Request(request.Email);
        return new MailedLink { LinkLifetimeHours = _tokens.TwoFactorRemovalTokenLifetimeHours };
    }

    public Task ScheduleRemoval(Guid secret) => _removalService.Schedule(secret);

    public Task CancelRemoval(Guid secret) => _removalService.Cancel(secret);

    public Task ClearForColleague(string username) => _removalService.ClearForColleague(username);
}
