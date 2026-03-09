using System.Threading.Tasks;

namespace DM.Web.API.Features.Account.Availability;

/// <summary>
/// API service for checking email and username availability
/// </summary>
public interface IAvailabilityApiService
{
    /// <summary>
    /// Check if email is available for registration
    /// </summary>
    /// <param name="email">Email to check</param>
    /// <returns>Availability status</returns>
    Task<EmailAvailabilityResponse> CheckEmailAvailability(string email);

    /// <summary>
    /// Check if username is available for registration
    /// </summary>
    /// <param name="username">Username to check</param>
    /// <returns>Availability status</returns>
    Task<UsernameAvailabilityResponse> CheckUsernameAvailability(string username);
}
