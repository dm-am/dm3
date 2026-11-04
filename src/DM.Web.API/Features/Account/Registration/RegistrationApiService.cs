using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Registration;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Features.Account.Registration;

/// <inheritdoc />
internal class RegistrationApiService : IRegistrationApiService
{
    private readonly IRegistrationService _registrationService;
    private readonly RegistrationMapper _mapper;
    private readonly TokenConfiguration _tokens;

    public RegistrationApiService(
        IRegistrationService registrationService,
        RegistrationMapper mapper,
        IOptions<TokenConfiguration> tokens)
    {
        _registrationService = registrationService;
        _mapper = mapper;
        _tokens = tokens.Value;
    }

    /// <inheritdoc />
    public async Task<MailedLink> Register(RegistrationRequest registration)
    {
        await _registrationService.Register(_mapper.ToUserRegistration(registration));
        return new MailedLink { LinkLifetimeHours = _tokens.ActivationTokenLifetimeHours };
    }
}
