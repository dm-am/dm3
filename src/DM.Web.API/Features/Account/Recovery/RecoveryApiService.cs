using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.PasswordChange;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Account.Recovery;

/// <inheritdoc />
internal class RecoveryApiService : IRecoveryApiService
{
    private readonly IRecoveryService _recoveryService;
    private readonly IPasswordChangeService _passwordChangeService;
    private readonly UserMapper _mapper;

    public RecoveryApiService(
        IRecoveryService recoveryService,
        IPasswordChangeService passwordChangeService,
        UserMapper mapper)
    {
        _recoveryService = recoveryService;
        _passwordChangeService = passwordChangeService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<RecoveryResponse> Recover(RecoveryRequest request)
    {
        var result = await _recoveryService.Recover(request.Email);

        return new RecoveryResponse
        {
            Status = result switch
            {
                RecoveryResult.PasswordReset => RecoveryStatus.PasswordResetSent,
                RecoveryResult.ActivationResent => RecoveryStatus.ActivationResent,
                // NotFound is also what an unknown enum value answers with:
                // recovery never tells the caller whether the address exists.
                _ => RecoveryStatus.NotFound
            },
            Email = request.Email
        };
    }

    /// <inheritdoc />
    public async Task<PasswordResetTokenInfo?> GetTokenInfo(Guid token)
    {
        var result = await _passwordChangeService.GetTokenInfo(token);
        if (result == null)
            return null;

        return new PasswordResetTokenInfo { Status = result.Status };
    }

    /// <inheritdoc />
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
