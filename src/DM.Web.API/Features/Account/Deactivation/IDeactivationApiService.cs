using System.Threading.Tasks;

namespace DM.Web.API.Features.Account.Deactivation;

/// <summary>
/// API service for account deactivation
/// </summary>
public interface IDeactivationApiService
{
    /// <summary>
    /// Deactivate the current user's account
    /// </summary>
    /// <param name="request">Deactivation request with password confirmation</param>
    Task Deactivate(DeactivationRequest request);
}
