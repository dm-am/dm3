using AutoMapper;
using DM.Domain.Account.Features.UsernameChange;
using ApiUsernameChangeRequest = DM.Web.API.Features.Moderation.UsernameChanges.UsernameChangeRequest;

namespace DM.Web.API.Features.Moderation.UsernameChanges;

/// <summary>
/// AutoMapper profile for the moderation queue of name change requests.
/// </summary>
/// <remarks>
/// The queue endpoints mapped without a map, so the page answered 500 to a
/// senior moderator the moment one request was waiting - and 200 with an empty
/// list while none were, which is how it passed for working. Three names differ
/// between the two shapes and are stated here rather than left to convention.
/// </remarks>
internal class UsernameChangeMappingProfile : Profile
{
    public UsernameChangeMappingProfile()
    {
        CreateMap<UsernameChangeRequestEntry, ApiUsernameChangeRequest>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.RequestId))
            .ForMember(d => d.ApprovalExpiresUtc, o => o.MapFrom(s => s.ApprovalTokenExpiresUtc))
            .ForMember(d => d.ResolvedBy, o => o.MapFrom(s => s.ResolvedByUsername))
            .ForMember(d => d.Comment, o => o.MapFrom(s => s.ResolverComment));
    }
}
