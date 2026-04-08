using AutoMapper;
using DM.Domain.Game.Features.GameReviews;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// AutoMapper profile for Game review API DTOs
/// </summary>
internal class GameReviewApiMappingProfile : Profile
{
    /// <inheritdoc />
    public GameReviewApiMappingProfile()
    {
        CreateMap<GameReview, GameReviewDto>();
    }
}
