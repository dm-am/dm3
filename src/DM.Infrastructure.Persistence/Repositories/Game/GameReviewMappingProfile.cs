using AutoMapper;
using DM.Domain.Game.Features.GameReviews;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// AutoMapper profile for game review mappings
/// </summary>
internal class GameReviewMappingProfile : Profile
{
    /// <inheritdoc />
    public GameReviewMappingProfile()
    {
        CreateMap<DbGameReview, GameReview>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.GameReviewId))
            .ForMember(d => d.GameTitle, s => s.MapFrom(r => r.Game.Title));
    }
}
