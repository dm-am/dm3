using System.Threading.Tasks;
using DM.Domain.Account.Features.Availability;

namespace DM.Web.API.Features.Account.Availability;

public class AvailabilityApiService
{
    private readonly IAvailabilityService _availabilityService;

    public AvailabilityApiService(IAvailabilityService availabilityService)
    {
        _availabilityService = availabilityService;
    }

    public async Task<EmailAvailabilityResponse> CheckEmailAvailability(string email)
    {
        var result = await _availabilityService.CheckEmailAvailability(email);
        return new EmailAvailabilityResponse { IsAvailable = result.IsAvailable, Reason = result.Reason };
    }

    public async Task<UsernameAvailabilityResponse> CheckUsernameAvailability(string username)
    {
        var result = await _availabilityService.CheckUsernameAvailability(username);
        return new UsernameAvailabilityResponse { IsAvailable = result.IsAvailable, Reason = result.Reason };
    }
}
