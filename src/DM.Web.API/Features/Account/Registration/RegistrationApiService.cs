using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Registration;

namespace DM.Web.API.Features.Account.Registration;

/// <inheritdoc />
internal class RegistrationApiService : IRegistrationApiService
{
    private readonly IRegistrationService _registrationService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public RegistrationApiService(
        IRegistrationService registrationService,
        IMapper mapper)
    {
        _registrationService = registrationService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task Register(RegistrationRequest registration) =>
        _registrationService.Register(_mapper.Map<UserRegistration>(registration));
}
