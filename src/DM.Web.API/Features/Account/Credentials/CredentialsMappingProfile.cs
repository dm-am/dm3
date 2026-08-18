using AutoMapper;
using DM.Domain.Account.Features.UsernameChange;

namespace DM.Web.API.Features.Account.Credentials;

/// <summary>
/// AutoMapper profile for account credential mappings.
/// </summary>
/// <remarks>
/// Without this map every endpoint of the name change answered 500: filing a
/// request wrote the row and then failed on the reply, reading one failed for
/// anyone who had ever filed, and finishing the change renamed the account and
/// failed on the reply too. Only an account that had never asked got an answer,
/// because that path returns no content and maps nothing.
/// </remarks>
internal class CredentialsMappingProfile : Profile
{
    public CredentialsMappingProfile()
    {
        CreateMap<UsernameChangeRequestEntry, UsernameChangeResponse>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.RequestId));
    }
}
