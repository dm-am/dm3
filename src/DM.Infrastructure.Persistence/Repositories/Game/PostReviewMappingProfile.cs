using AutoMapper;
using DM.Domain.Game.Features.PostReviews;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Profile for post review DTO and DAL mapping
/// </summary>
internal class PostReviewMappingProfile : Profile
{
    public PostReviewMappingProfile()
    {
        // DbPostReview -> PostReview (domain DTO)
        CreateMap<DbPostReview, PostReview>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.PostReviewId))
            .ForMember(d => d.Author, s => s.MapFrom(r => r.Author))
            .ForMember(d => d.PostId, s => s.MapFrom(r => r.PostId))
            .ForMember(d => d.PostAuthor, s => s.MapFrom(r => r.PostAuthor))
            .ForMember(d => d.GameId, s => s.MapFrom(r => r.GameId))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(r => r.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(r => r.ModifiedUtc))
            .ForMember(d => d.Text, s => s.MapFrom(r => r.Text))
            .ForMember(d => d.Sign, s => s.MapFrom(r => r.Sign));
    }
}
