using DM.Domain.Account.Features.Registration;
using Riok.Mapperly.Abstractions;

namespace DM.Web.API.Features.Account.Registration;

/// <summary>
/// Compile-time mapper for registration
/// </summary>
[Mapper]
internal partial class RegistrationMapper
{
    /// <summary>
    /// Registration request to the domain registration command. The honeypot
    /// field stays behind on purpose: the refusal check reads it from the
    /// request, and the domain command has no business carrying bot bait.
    /// </summary>
    [MapperIgnoreSource(nameof(RegistrationRequest.Website))]
    public partial UserRegistration ToUserRegistration(RegistrationRequest registration);
}
