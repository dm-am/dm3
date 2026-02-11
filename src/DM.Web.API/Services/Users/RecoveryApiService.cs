using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Account.Recovery;
using DM.Web.API.Dto.Users;
using ServiceEmailReason = DM.Services.Community.BusinessProcesses.Account.Recovery.EmailUnavailableReason;

namespace DM.Web.API.Services.Users;

/// <inheritdoc />
internal class RecoveryApiService : IRecoveryApiService
{
    private readonly IRecoveryService _recoveryService;

    public RecoveryApiService(IRecoveryService recoveryService)
    {
        _recoveryService = recoveryService;
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
        var result = await _recoveryService.CheckEmailAvailability(email);

        return new EmailAvailabilityResponse
        {
            Available = result.Available,
            Reason = result.Reason switch
            {
                ServiceEmailReason.Taken => Dto.Users.EmailUnavailableReason.Taken,
                ServiceEmailReason.PendingActivation => Dto.Users.EmailUnavailableReason.PendingActivation,
                null => null,
                _ => null
            }
        };
    }
}
