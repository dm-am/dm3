using AutoMapper;
using DbReview = DM.Services.DataAccess.BusinessObjects.Common.Review;

namespace DM.Services.Community.BusinessProcesses.Reviews.Reading;

/// <inheritdoc />
internal class ReviewProfile : Profile
{
    /// <inheritdoc />
    public ReviewProfile()
    {
        CreateMap<DbReview, Review>()
            .ForMember(d => d.Id, s => s.MapFrom(r => r.ReviewId))
            .ForMember(d => d.Approved, s => s.MapFrom(r => r.IsApproved))
            .ForMember(d => d.TargetType, s => s.MapFrom(r => r.TargetType))
            .ForMember(d => d.TargetId, s => s.MapFrom(r => r.TargetId))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(r => r.ModifiedUtc))
            // Post review specific fields
            .ForMember(d => d.Sign, s => s.MapFrom(r => r.Sign))
            .ForMember(d => d.ReasonType, s => s.MapFrom(r => r.ReasonType))
            .ForMember(d => d.PostAuthorId, s => s.MapFrom(r => r.PostAuthorId))
            .ForMember(d => d.GameId, s => s.MapFrom(r => r.GameId))
            // TargetUser, TargetGameTitle, PostAuthor are populated manually in repository
            .ForMember(d => d.TargetUser, s => s.Ignore())
            .ForMember(d => d.TargetGameTitle, s => s.Ignore())
            .ForMember(d => d.PostAuthor, s => s.Ignore());
    }
}