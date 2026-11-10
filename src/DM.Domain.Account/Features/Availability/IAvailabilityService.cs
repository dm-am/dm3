using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Availability;

/// <summary>
/// Service for checking email and username availability
/// </summary>
public interface IAvailabilityService
{
    Task<EmailAvailabilityResult> CheckEmailAvailability(string email, CancellationToken cancellationToken = default);

    Task<UsernameAvailabilityResult> CheckUsernameAvailability(string username, CancellationToken cancellationToken = default);
}
