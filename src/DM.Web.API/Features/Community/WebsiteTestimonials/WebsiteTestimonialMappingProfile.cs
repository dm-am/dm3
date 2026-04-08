using AutoMapper;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Community.WebsiteTestimonials;

/// <inheritdoc />
internal class WebsiteTestimonialMappingProfile : Profile
{
    /// <inheritdoc />
    public WebsiteTestimonialMappingProfile()
    {
        CreateMap<DM.Domain.Community.Features.WebsiteTestimonials.WebsiteTestimonial, WebsiteTestimonialDto>()
            .ForMember(d => d.Author, s => s.MapFrom(t => t.Author != null ? new User
            {
                Id = t.Author.UserId,
                Username = t.Author.Username,
                Role = t.Author.Role,
                Rating = new Rating { TotalPosts = t.Author.QuantityRating, PostReviewScoreSum = t.Author.QualityRating },
                Picture = new UserPicture { SmallUrl = t.Author.SmallPictureUrl }
            } : null));
    }
}
