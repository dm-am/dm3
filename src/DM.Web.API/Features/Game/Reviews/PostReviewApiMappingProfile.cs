using AutoMapper;
using DM.Domain.Game.Features.PostReviews;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// AutoMapper profile for post review API DTOs
/// </summary>
internal class PostReviewApiMappingProfile : Profile
{
    /// <inheritdoc />
    public PostReviewApiMappingProfile()
    {
        CreateMap<PostReview, PostReviewDto>();
    }
}
