using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Service for user registration
/// </summary>
public interface IRegistrationService
{
    Task Register(UserRegistration registration);
}
