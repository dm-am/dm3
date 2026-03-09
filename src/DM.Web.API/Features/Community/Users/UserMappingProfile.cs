using AutoMapper;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// AutoMapper profile for user mappings.
/// </summary>
internal class UserMappingProfile : Profile
{
    private const int NewbieThreshold = 100;

    public UserMappingProfile()
    {
        // GeneralUser (domain) -> User (API)
        CreateMap<GeneralUser, User>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.UserId))
            .ForMember(d => d.IsNewbie, o => o.MapFrom(s => s.QuantityRating < NewbieThreshold))
            .ForMember(d => d.Rating, o => o.MapFrom(s => s.RatingDisabled
                ? null
                : new Rating { TotalPosts = s.QuantityRating, PostReviewScoreSum = s.QualityRating }))
            .ForMember(d => d.Picture, o => o.MapFrom(s => new UserPicture { SmallUrl = s.SmallPictureUrl }))
            .ForMember(d => d.UsernameHistory, o => o.Ignore());

        // AuthenticatedUser (domain) -> User (API) - inherits from GeneralUser
        CreateMap<AuthenticatedUser, User>()
            .IncludeBase<GeneralUser, User>();

        // GeneralUser (domain) -> UserProfile (API)
        CreateMap<GeneralUser, UserProfile>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.UserId))
            .ForMember(d => d.IsNewbie, o => o.MapFrom(s => s.QuantityRating < NewbieThreshold))
            .ForMember(d => d.Rating, o => o.MapFrom(s => new Rating { TotalPosts = s.QuantityRating, PostReviewScoreSum = s.QualityRating }))
            .ForMember(d => d.Picture, o => o.MapFrom(s => new UserPicture
            {
                SmallUrl = s.SmallPictureUrl,
                MediumUrl = s.MediumPictureUrl
            }))
            .ForMember(d => d.Birthday, o => o.MapFrom(s => s.ShowBirthday && s.BirthdayDate.HasValue
                ? new Birthday { Day = s.BirthdayDate.Value.Day, Month = s.BirthdayDate.Value.Month, Year = s.BirthdayDate.Value.Year }
                : null))
            .ForMember(d => d.PostReviewsGiven, o => o.MapFrom(s => s.PostReviewsGivenCount))
            .ForMember(d => d.PostReviewsReceived, o => o.MapFrom(s => s.PostReviewsReceivedCount))
            .ForMember(d => d.UsernameHistory, o => o.Ignore())
            .ForMember(d => d.FeaturedPost, o => o.Ignore())
            .ForMember(d => d.Contacts, o => o.Ignore())
            .ForMember(d => d.Info, o => o.Ignore())
            .ForMember(d => d.RegisteredAtUtc, o => o.Ignore());
    }
}
