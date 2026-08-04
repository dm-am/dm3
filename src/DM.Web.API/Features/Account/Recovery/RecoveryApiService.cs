using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Recovery;
using DM.Domain.Account.Features.PasswordChange;
using DM.Domain.Account.Features.Availability;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Account.Availability;
using ServiceEmailReason = DM.Domain.Account.Features.Availability.EmailUnavailableReason;
using ApiEmailUnavailableReason = DM.Web.API.Features.Account.Availability.EmailUnavailableReason;

namespace DM.Web.API.Features.Account.Recovery;

/// <inheritdoc />
internal class RecoveryApiService : IRecoveryApiService
{
    private readonly IRecoveryService _recoveryService;
    private readonly IPasswordChangeService _passwordChangeService;
    private readonly IAvailabilityService _availabilityService;
    private readonly IMapper _mapper;

    public RecoveryApiService(
        IRecoveryService recoveryService,
        IPasswordChangeService passwordChangeService,
        IAvailabilityService availabilityService,
        IMapper mapper)
    {
        _recoveryService = recoveryService;
        _passwordChangeService = passwordChangeService;
        _availabilityService = availabilityService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<RecoveryResponse> Recover(RecoveryRequest request)
    {
        var result = await _recoveryService.Recover(request.Email);

        return result switch
        {
            RecoveryResult.PasswordReset => new RecoveryResponse
            {
                Status = RecoveryStatus.PasswordResetSent,
                Email = request.Email
            },
            RecoveryResult.ActivationResent => new RecoveryResponse
            {
                Status = RecoveryStatus.ActivationResent,
                Email = request.Email
            },
            RecoveryResult.NotFound => new RecoveryResponse
            {
                Status = RecoveryStatus.NotFound,
                Email = request.Email
            },
            _ => new RecoveryResponse
            {
                Status = RecoveryStatus.NotFound,
                Email = request.Email
            }
        };
    }

    /// <inheritdoc />
    public async Task<EmailAvailabilityResponse> CheckEmailAvailability(string email)
    {
        var result = await _availabilityService.CheckEmailAvailability(email);

        return new EmailAvailabilityResponse
        {
            IsAvailable = result.IsAvailable,
            Reason = result.Reason switch
            {
                ServiceEmailReason.Taken => ApiEmailUnavailableReason.Taken,
                ServiceEmailReason.PendingActivation => ApiEmailUnavailableReason.PendingActivation,
                null => null
            }
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
        return _mapper.Map<User>(user);
    }
}
