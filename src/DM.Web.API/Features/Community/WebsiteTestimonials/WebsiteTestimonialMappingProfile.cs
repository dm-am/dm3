using AutoMapper;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Community.WebsiteTestimonials;

/// <inheritdoc />
internal class WebsiteTestimonialMappingProfile : Profile
{
    /// <inheritdoc />
    public WebsiteTestimonialMappingProfile()
    {
        // Author = GeneralUser → User reuses the existing UserMappingProfile map
        // (which includes the AvatarPicture → UserPicture conversion via
        // AvatarPictureConverter — single source of truth for avatar URLs).
        CreateMap<DM.Domain.Community.Features.WebsiteTestimonials.WebsiteTestimonial, WebsiteTestimonialDto>()
            .ForMember(d => d.Author, opt => opt.MapFrom(t => t.Author));
    }
}
