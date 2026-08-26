using System.Threading.Tasks;
using DM.Domain.Account.Features.Registration;

namespace DM.Web.API.Features.Account.Registration;

/// <inheritdoc />
internal class RegistrationApiService : IRegistrationApiService
{
    private readonly IRegistrationService _registrationService;
    private readonly RegistrationMapper _mapper;

    /// <inheritdoc />
    public RegistrationApiService(
        IRegistrationService registrationService,
        RegistrationMapper mapper)
    {
        _registrationService = registrationService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task Register(RegistrationRequest registration) =>
        _registrationService.Register(_mapper.ToUserRegistration(registration));
}
