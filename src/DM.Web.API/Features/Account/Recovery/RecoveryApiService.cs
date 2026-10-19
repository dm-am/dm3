using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.PasswordChange;
using DM.Web.API.Features.Community.Users;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Features.Account.Recovery;

public class RecoveryApiService
{
    private readonly IRecoveryService _recoveryService;
    private readonly IPasswordChangeService _passwordChangeService;
    private readonly UserMapper _mapper;
    private readonly TokenConfiguration _tokens;

    public RecoveryApiService(
        IRecoveryService recoveryService,
        IPasswordChangeService passwordChangeService,
        UserMapper mapper,
        IOptions<TokenConfiguration> tokens)
    {
        _recoveryService = recoveryService;
        _passwordChangeService = passwordChangeService;
        _mapper = mapper;
        _tokens = tokens.Value;
    }

    public async Task<RecoveryResponse> Recover(RecoveryRequest request)
    {
        var result = await _recoveryService.Recover(request.Email);

        return new RecoveryResponse
        {
            Status = result switch
            {
                RecoveryResult.PasswordReset => RecoveryStatus.PasswordResetSent,
                RecoveryResult.ActivationResent => RecoveryStatus.ActivationResent,
                // An unknown value must not read as a letter that went out.
                _ => RecoveryStatus.NotFound
            },
            Email = request.Email,
            LinkLifetimeHours = result switch
            {
                RecoveryResult.PasswordReset => _tokens.PasswordResetTokenLifetimeHours,
                RecoveryResult.ActivationResent => _tokens.ActivationTokenLifetimeHours,
                _ => null
            }
        };
    }

    public async Task<PasswordResetTokenInfo?> GetTokenInfo(Guid token)
    {
        var result = await _passwordChangeService.GetTokenInfo(token);
        if (result == null)
            return null;

        return new PasswordResetTokenInfo { Status = result.Status };
    }

    public async Task<User> ResetPassword(Guid token, PasswordResetCompletion request)
    {
        var passwordChange = new UserPasswordChange
        {
            Token = token,
            NewPassword = request.NewPassword
        };

        var user = await _passwordChangeService.Change(passwordChange);
        return _mapper.ToUser(user);
    }
}
