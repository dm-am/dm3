using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Availability;

/// <summary>
/// Service for checking email and username availability
/// </summary>
public interface IAvailabilityService
{
    /// <summary>
    /// Check if email is available for registration
    /// </summary>
    /// <param name="email">Email to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Availability result</returns>
    Task<EmailAvailabilityResult> CheckEmailAvailability(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if username is available for registration
    /// </summary>
    /// <param name="username">Username to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Availability result</returns>
    Task<UsernameAvailabilityResult> CheckUsernameAvailability(string username, CancellationToken cancellationToken = default);
}
