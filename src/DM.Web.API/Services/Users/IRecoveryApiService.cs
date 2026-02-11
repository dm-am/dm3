using System.Threading.Tasks;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Services.Users;

/// <summary>
/// API service for account recovery operations
/// </summary>
public interface IRecoveryApiService
{
    /// <summary>
    /// Process account recovery request
    /// </summary>
    /// <param name="request">Recovery request with email</param>
    /// <returns>Recovery result</returns>
    Task<RecoveryResponse> Recover(RecoveryRequest request);

    /// <summary>
    /// Check if email is available for registration
    /// </summary>
    /// <param name="email">Email to check</param>
    /// <returns>Availability status</returns>
    Task<EmailAvailabilityResponse> CheckEmailAvailability(string email);
}
