using System.Threading.Tasks;

namespace DM.Web.API.Features.Account.Registration;

/// <summary>
/// API service for registration
/// </summary>
public interface IRegistrationApiService
{
    /// <summary>
    /// Register new user
    /// </summary>
    /// <param name="registration">Registration information</param>
    Task Register(RegistrationRequest registration);
}
