using AutoMapper;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DomainUsernameHistory = DM.Domain.Core.Users.UsernameHistoryEntry;
using DomainModuleStatusCounts = DM.Domain.Core.Dto.ModuleStatusCounts;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// AutoMapper profile for user mappings.
/// </summary>
internal class UserMappingProfile : Profile
{
    private const int NewbieThreshold = 100;

    public UserMappingProfile()
    {
        // Domain ModuleStatusCounts -> API ModuleStatusCounts
        CreateMap<DomainModuleStatusCounts, ModuleStatusCounts>();

        // Domain UsernameHistoryEntry -> API UsernameHistoryEntry
        CreateMap<DomainUsernameHistory, UsernameHistoryEntry>()
            .ForMember(d => d.OldUsername, o => o.MapFrom(s => s.OldUsername))
            .ForMember(d => d.ChangedUtc, o => o.MapFrom(s => s.ChangedUtc));

        // GeneralUser (domain) -> User (API)
        CreateMap<GeneralUser, User>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.UserId))
            .ForMember(d => d.IsNewbie, o => o.MapFrom(s => s.QuantityRating < NewbieThreshold))
            .ForMember(d => d.Rating, o => o.MapFrom(s => s.RatingDisabled
                ? null
                : new Rating { TotalPosts = s.QuantityRating, PostReviewScoreSum = s.QualityRating }))
            .ForMember(d => d.Picture, o => o.MapFrom(s => new UserPicture { SmallUrl = s.SmallPictureUrl }))
            .ForMember(d => d.UsernameHistory, o => o.MapFrom(s => s.UsernameHistory))
            // Statistics for community list
            .ForMember(d => d.ReviewsGiven, o => o.MapFrom(s => s.PostReviewsGivenCount))
            .ForMember(d => d.ReviewsReceived, o => o.MapFrom(s => s.PostReviewsReceivedCount));

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
            .ForMember(d => d.PersonalNote, o => o.Ignore())
            .ForMember(d => d.BestPost, o => o.Ignore())
            .ForMember(d => d.Contacts, o => o.Ignore())
            .ForMember(d => d.Info, o => o.Ignore())
            // Statistics for community list (inherited from User)
            .ForMember(d => d.ReviewsGiven, o => o.MapFrom(s => s.PostReviewsGivenCount))
            .ForMember(d => d.ReviewsReceived, o => o.MapFrom(s => s.PostReviewsReceivedCount));
    }
}
