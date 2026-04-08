using AutoMapper;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Community.Endorsements;

/// <inheritdoc />
internal class UserEndorsementMappingProfile : Profile
{
    /// <inheritdoc />
    public UserEndorsementMappingProfile()
    {
        CreateMap<DM.Domain.Community.Features.UserEndorsements.UserEndorsement, UserEndorsement>()
            .ForMember(d => d.Author, s => s.MapFrom(e => e.Author != null ? new User
            {
                Id = e.Author.UserId,
                Username = e.Author.Username,
                Role = e.Author.Role,
                Rating = new Rating { TotalPosts = e.Author.QuantityRating, PostReviewScoreSum = e.Author.QualityRating },
                Picture = new UserPicture { SmallUrl = e.Author.SmallPictureUrl }
            } : null))
            .ForMember(d => d.TargetUser, s => s.MapFrom(e => e.TargetUser != null ? new User
            {
                Id = e.TargetUser.UserId,
                Username = e.TargetUser.Username,
                Role = e.TargetUser.Role,
                Rating = new Rating { TotalPosts = e.TargetUser.QuantityRating, PostReviewScoreSum = e.TargetUser.QualityRating },
                Picture = new UserPicture { SmallUrl = e.TargetUser.SmallPictureUrl }
            } : null));
    }
}
