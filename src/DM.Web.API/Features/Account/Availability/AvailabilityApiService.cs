using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;
using ServiceEmailReason = DM.Domain.Account.Features.Availability.EmailUnavailableReason;
using ServiceUsernameReason = DM.Domain.Account.Features.Availability.UsernameUnavailableReason;

namespace DM.Web.API.Features.Account.Availability;

/// <inheritdoc />
internal class AvailabilityApiService : IAvailabilityApiService
{
    private readonly IAvailabilityService _availabilityService;

    public AvailabilityApiService(IAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
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
                ServiceEmailReason.Taken => EmailUnavailableReason.Taken,
                ServiceEmailReason.PendingActivation => EmailUnavailableReason.PendingActivation,
                null => null
            }
        };
    }

    /// <inheritdoc />
    public async Task<UsernameAvailabilityResponse> CheckUsernameAvailability(string username)
    {
        var result = await _availabilityService.CheckUsernameAvailability(username);

        return new UsernameAvailabilityResponse
        {
            IsAvailable = result.IsAvailable,
            Reason = result.Reason switch
            {
                ServiceUsernameReason.Taken => UsernameUnavailableReason.Taken,
                ServiceUsernameReason.Reserved => UsernameUnavailableReason.Reserved,
                ServiceUsernameReason.InvalidFormat => UsernameUnavailableReason.InvalidFormat,
                null => null
            }
        };
    }
}
