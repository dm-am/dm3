using AutoMapper;
using DM.Domain.Core.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DomainUsernameHistory = DM.Domain.Core.Users.UsernameHistoryEntry;
using DomainUserContact = DM.Domain.Core.Users.UserContact;
using DomainModuleStatusCounts = DM.Domain.Core.Dto.ModuleStatusCounts;
using DomainSubscriberInfo = DM.Domain.Core.Dto.SubscriberInfo;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// AutoMapper profile for user mappings.
/// </summary>
internal class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        // Avatar: Domain AvatarPicture (single source-key) → API UserPicture (3 URLs).
        // The converter builds imgproxy thumbnails on the fly via
        // AvatarPictureConverter (see the sibling file) — needs IImgproxyUrlBuilder DI.
        CreateMap<DM.Domain.Core.Dto.AvatarPicture, UserPicture>()
            .ConvertUsing<AvatarPictureConverter>();

        // Domain ModuleStatusCounts -> API ModuleStatusCounts
        CreateMap<DomainModuleStatusCounts, ModuleStatusCounts>();

        // Domain UsernameHistoryEntry -> API UsernameHistoryEntry
        CreateMap<DomainUsernameHistory, UsernameHistoryEntry>()
            .ForMember(d => d.OldUsername, o => o.MapFrom(s => s.OldUsername))
            .ForMember(d => d.ChangedUtc, o => o.MapFrom(s => s.ChangedUtc));

        // Domain UserContact -> API Contact (field rename: ContactValue -> Value)
        CreateMap<DomainUserContact, Contact>()
            .ForMember(d => d.ContactType, o => o.MapFrom(s => s.ContactType))
            .ForMember(d => d.Value, o => o.MapFrom(s => s.ContactValue));

        // Domain SubscriberInfo -> API SubscriberRef (1:1 fields)
        CreateMap<DomainSubscriberInfo, SubscriberRef>();

        // Domain SubscribersByCategory -> API SubscriberCounts (1:1 fields). One
        // map covers the member on User, UserProfile, PersonalProfile and
        // ModeratedProfile — they all inherit it.
        CreateMap<DM.Domain.Core.Dto.SubscribersByCategory, SubscriberCounts>();

        // Query string to domain filter. Mapped by name rather than by hand so a
        // filter added to both sides needs no third edit here, and so the two
        // members that do not line up have to be named to be handled: the search
        // term is Q on the wire, and the direction is derived from SortOrder by
        // CommunityUserApiService, which knows the per-field default.
        CreateMap<UsersQuery, UserFilter>()
            .ForMember(d => d.Search, o => o.MapFrom(s => s.Q))
            .ForMember(d => d.SortAscending, o => o.Ignore());

        // GeneralUser (domain) -> User (API)
        CreateMap<GeneralUser, User>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.UserId))
            .ForMember(d => d.IsNewbie, o => o.MapFrom(s => s.QuantityRating < ProbationPolicy.NewbiePostThreshold))
            .ForMember(d => d.Rating, o => o.MapFrom(s => s.RatingDisabled
                ? null
                : new Rating { TotalPosts = s.QuantityRating, PostReviewScoreSum = s.QualityRating }))
            // Lists (User DTO) only expose SmallUrl. Profile page calls
            // /v1/users/{username}/profile → UserProfile mapping below adds
            // MediumUrl for retina-quality avatars.
            // Picture — via the registered AvatarPicture→UserPicture converter
            // (imgproxy thumbnails on the fly). Lists and the profile page get
            // the same structure (3 URLs); the ~200 bytes/user bandwidth cost is negligible.
            .ForMember(d => d.Picture, o => o.MapFrom(s => s.Picture))
            .ForMember(d => d.UsernameHistory, o => o.MapFrom(s => s.UsernameHistory))
            // Statistics for community list
            .ForMember(d => d.ReviewsGiven, o => o.MapFrom(s => s.PostReviewsGivenCount))
            .ForMember(d => d.ReviewsReceived, o => o.MapFrom(s => s.PostReviewsReceivedCount))
            .ForMember(d => d.EndorsementsGiven, o => o.MapFrom(s => s.EndorsementsGivenCount))
            .ForMember(d => d.EndorsementsReceived, o => o.MapFrom(s => s.EndorsementsReceivedCount))
            .ForMember(d => d.TopicsAuthored, o => o.MapFrom(s => s.TopicsAuthoredCount))
            .ForMember(d => d.CommentsAuthored, o => o.MapFrom(s => s.CommentsAuthoredCount))
            .ForMember(d => d.GlobalChatMessages, o => o.MapFrom(s => s.GlobalChatMessagesCount))
            .ForMember(d => d.BansReceived, o => o.MapFrom(s => s.BansReceivedCount))
            .ForMember(d => d.GameDrops, o => o.MapFrom(s => s.GameDropsCount))
            .ForMember(d => d.PublicationsAuthored, o => o.MapFrom(s => s.PublicationsAuthoredCount))
            .ForMember(d => d.LikesReceived, o => o.MapFrom(s => s.LikesReceivedCount));

        // AuthenticatedUser (domain) -> User (API) - inherits from GeneralUser
        CreateMap<AuthenticatedUser, User>()
            .IncludeBase<GeneralUser, User>();

        // GeneralUser (domain) -> UserProfile (API)
        CreateMap<GeneralUser, UserProfile>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.UserId))
            .ForMember(d => d.IsNewbie, o => o.MapFrom(s => s.QuantityRating < ProbationPolicy.NewbiePostThreshold))
            .ForMember(d => d.Rating, o => o.MapFrom(s => new Rating { TotalPosts = s.QuantityRating, PostReviewScoreSum = s.QualityRating }))
            // Picture — via the registered AvatarPicture→UserPicture converter.
            .ForMember(d => d.Picture, o => o.MapFrom(s => s.Picture))
            .ForMember(d => d.Birthday, o => o.MapFrom(s => s.ShowBirthday && s.BirthdayDate.HasValue
                ? new Birthday { Day = s.BirthdayDate.Value.Day, Month = s.BirthdayDate.Value.Month, Year = s.BirthdayDate.Value.Year }
                : null))
            .ForMember(d => d.PostReviewsGiven, o => o.MapFrom(s => s.PostReviewsGivenCount))
            .ForMember(d => d.PostReviewsReceived, o => o.MapFrom(s => s.PostReviewsReceivedCount))
            .ForMember(d => d.UsernameHistory, o => o.Ignore())
            .ForMember(d => d.PersonalNote, o => o.Ignore())
            .ForMember(d => d.Contacts, o => o.Ignore())
            .ForMember(d => d.Info, o => o.Ignore())
            // Statistics for community list (inherited from User)
            .ForMember(d => d.ReviewsGiven, o => o.MapFrom(s => s.PostReviewsGivenCount))
            .ForMember(d => d.ReviewsReceived, o => o.MapFrom(s => s.PostReviewsReceivedCount))
            .ForMember(d => d.EndorsementsGiven, o => o.MapFrom(s => s.EndorsementsGivenCount))
            .ForMember(d => d.EndorsementsReceived, o => o.MapFrom(s => s.EndorsementsReceivedCount))
            .ForMember(d => d.TopicsAuthored, o => o.MapFrom(s => s.TopicsAuthoredCount))
            .ForMember(d => d.CommentsAuthored, o => o.MapFrom(s => s.CommentsAuthoredCount))
            .ForMember(d => d.GlobalChatMessages, o => o.MapFrom(s => s.GlobalChatMessagesCount))
            .ForMember(d => d.BansReceived, o => o.MapFrom(s => s.BansReceivedCount))
            .ForMember(d => d.GameDrops, o => o.MapFrom(s => s.GameDropsCount))
            .ForMember(d => d.PublicationsAuthored, o => o.MapFrom(s => s.PublicationsAuthoredCount))
            .ForMember(d => d.LikesReceived, o => o.MapFrom(s => s.LikesReceivedCount));
    }
}
