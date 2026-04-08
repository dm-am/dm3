using AutoMapper;
using DtoTagGroup = DM.Domain.Moderation.Features.Tags.TagGroup;
using DtoTag = DM.Domain.Moderation.Features.Tags.Tag;
using DtoCreateTagGroup = DM.Domain.Moderation.Features.Tags.CreateTagGroup;
using DtoUpdateTagGroup = DM.Domain.Moderation.Features.Tags.UpdateTagGroup;
using DtoCreateTag = DM.Domain.Moderation.Features.Tags.CreateTag;
using DtoUpdateTag = DM.Domain.Moderation.Features.Tags.UpdateTag;

namespace DM.Web.API.Features.Moderation.Tags;

internal class TagMappingProfile : Profile
{
    public TagMappingProfile()
    {
        CreateMap<DtoTagGroup, TagGroup>();
        CreateMap<DtoTag, Tag>();

        CreateMap<CreateTagGroupRequest, DtoCreateTagGroup>();
        CreateMap<UpdateTagGroupRequest, DtoUpdateTagGroup>();
        CreateMap<CreateTagRequest, DtoCreateTag>();
        CreateMap<UpdateTagRequest, DtoUpdateTag>();
    }
}
