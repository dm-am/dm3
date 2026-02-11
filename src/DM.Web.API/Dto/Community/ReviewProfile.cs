using AutoMapper;
using DM.Services.Community.BusinessProcesses.Reviews.Creating;
using DM.Services.Community.BusinessProcesses.Reviews.Updating;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Community;

/// <inheritdoc />
internal class ReviewProfile : Profile
{
    /// <inheritdoc />
    public ReviewProfile()
    {
        CreateMap<DM.Services.Community.BusinessProcesses.Reviews.Reading.Review, Review>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(r => r.CreatedUtc))
            .ForMember(d => d.IsApproved, s => s.MapFrom(r => r.Approved))
            .ForMember(d => d.AuthorId, s => s.MapFrom(r => r.Author != null ? r.Author.UserId : default))
            .ForMember(d => d.AuthorLogin, s => s.MapFrom(r => r.Author != null ? r.Author.Login : null))
            .ForMember(d => d.Author, s => s.MapFrom(r => r.Author != null ? new UserSummary
            {
                Id = r.Author.UserId,
                Login = r.Author.Login,
                PrimaryRole = r.Author.Role,
                Rating = new UserRating(),
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

        CreateMap<Review, CreateReview>();
        CreateMap<Review, UpdateReview>()
            .ForMember(d => d.ReviewId, s => s.MapFrom(r => r.Id));
    }
}