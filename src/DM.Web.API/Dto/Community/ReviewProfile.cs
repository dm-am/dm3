using AutoMapper;
using DM.Services.Community.BusinessProcesses.Reviews.Creating;
using DM.Services.Community.BusinessProcesses.Reviews.Updating;

namespace DM.Web.API.Dto.Community;

/// <inheritdoc />
internal class ReviewProfile : Profile
{
    /// <inheritdoc />
    public ReviewProfile()
    {
        CreateMap<DM.Services.Community.BusinessProcesses.Reviews.Reading.Review, Review>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(r => r.CreateDate))
            .ForMember(d => d.IsApproved, s => s.MapFrom(r => r.Approved));
        CreateMap<Review, CreateReview>();
        CreateMap<Review, UpdateReview>()
            .ForMember(d => d.ReviewId, s => s.MapFrom(r => r.Id));
    }
}