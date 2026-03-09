using AutoMapper;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Community.Reviews;

/// <inheritdoc />
internal class ReviewMappingProfile : Profile
{
    /// <inheritdoc />
    public ReviewMappingProfile()
    {
        CreateMap<DM.Domain.Core.Reviews.Review, Review>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(r => r.CreatedUtc))
            .ForMember(d => d.IsApproved, s => s.MapFrom(r => r.Approved))
            .ForMember(d => d.AuthorId, s => s.MapFrom(r => r.Author != null ? r.Author.UserId : default))
            .ForMember(d => d.AuthorLogin, s => s.MapFrom(r => r.Author != null ? r.Author.Username : null))
            .ForMember(d => d.Author, s => s.MapFrom(r => r.Author != null ? new User
            {
                Id = r.Author.UserId,
                Username = r.Author.Username,
                Role = r.Author.Role,
                Rating = new Rating { TotalPosts = r.Author.QuantityRating, PostReviewScoreSum = r.Author.QualityRating },
                Picture = new UserPicture { SmallUrl = r.Author.SmallPictureUrl }
            } : null))
            .ForMember(d => d.TargetType, s => s.MapFrom(r => r.TargetType))
            .ForMember(d => d.TargetId, s => s.MapFrom(r => r.TargetId))
            .ForMember(d => d.LastUpdateUtc, s => s.MapFrom(r => r.ModifiedUtc))
            // Post review fields - not used for Platform/User/Game reviews
            .ForMember(d => d.Sign, s => s.Ignore())
            .ForMember(d => d.ReasonType, s => s.Ignore())
            .ForMember(d => d.TargetOwnerId, s => s.Ignore())
            .ForMember(d => d.ParentEntityId, s => s.Ignore());
    }
}
