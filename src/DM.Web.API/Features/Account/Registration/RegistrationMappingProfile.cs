using AutoMapper;
using DM.Domain.Account.Features.Registration;

namespace DM.Web.API.Features.Account.Registration;

/// <summary>
/// AutoMapper profile for registration mappings.
/// </summary>
internal class RegistrationMappingProfile : Profile
{
    public RegistrationMappingProfile()
    {
        CreateMap<RegistrationRequest, UserRegistration>();
    }
}
