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
    /// <returns>How long the activation link in the letter lives</returns>
    Task<MailedLink> Register(RegistrationRequest registration);
}
