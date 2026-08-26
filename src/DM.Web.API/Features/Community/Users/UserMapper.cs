using DM.Domain.Core.Identity;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Uploads;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Web.API.Features.Moderation.Profiles;
using Riok.Mapperly.Abstractions;
using DomainModuleStatusCounts = DM.Domain.Core.Dto.ModuleStatusCounts;
using DomainSubscriberInfo = DM.Domain.Core.Dto.SubscriberInfo;
using DomainUserContact = DM.Domain.Core.Users.UserContact;
using DomainUserFilter = DM.Domain.Core.Users.UserFilter;
using DomainUsernameHistory = DM.Domain.Core.Users.UsernameHistoryEntry;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// Compile-time mapper for the full user DTO: imgproxy comes through the
/// constructor, and other mappers compose this one via <c>[UseMapper]</c>.
/// A narrowing projection by design, so only target members are required.
/// </summary>
[Mapper]
internal partial class UserMapper
{
    /// <summary>
    /// Small thumbnail size - for inline avatars in lists/chat (24-64 px DOM).
    /// Source of truth for thumbnail sizes at the API layer.
    /// </summary>
    public const int SmallAvatarSize = 100;

    /// <summary>Medium thumbnail size - for card views (60-200 px DOM) + retina DPR.</summary>
    public const int MediumAvatarSize = 400;

    private readonly IImgproxyUrlBuilder _imgproxy;

    public UserMapper(IImgproxyUrlBuilder imgproxy)
    {
        _imgproxy = imgproxy;
    }

    /// <summary>
    /// General user to the list-level user DTO
    /// </summary>
    public User ToUser(GeneralUser user)
    {
        // Domain DTOs hand users over from `= null!` members, so a declared
        // non-null argument still arrives null at runtime. AutoMapper mapped
        // that null to null and consumers rely on it; every composed mapper
        // inherits the tolerance from this single guard.
        if (user == null)
        {
            return null!;
        }

        var result = ToUserCore(user);
        // A user may switch the rating off; the DTO then carries null rather
        // than a zeroed block, and the client renders "rating disabled".
        result.Rating = user.RatingDisabled
            ? null
            : new Rating { TotalPosts = user.QuantityRating, PostReviewScoreSum = user.QualityRating };
        return result;
    }

    /// <summary>
    /// Authenticated user to the list-level user DTO. The domain type only
    /// adds session state on top of GeneralUser, none of it is response
    /// material - the base mapping is the whole mapping.
    /// </summary>
    public User ToUser(AuthenticatedUser user) => ToUser((GeneralUser)user);

    /// <summary>
    /// General user to the profile-page DTO. Unlike the list-level
    /// <see cref="ToUser(GeneralUser)"/>, the rating is always present here,
    /// and the birthday honors the owner's showBirthday setting.
    /// </summary>
    public UserProfile ToUserProfile(GeneralUser user)
    {
        if (user == null)
        {
            return null!;
        }

        var result = ToUserProfileCore(user);
        FillProfileComputed(result, user);
        return result;
    }

    /// <summary>
    /// General user to the moderated view of the profile. The same member
    /// block as <see cref="ToUserProfile"/>; the moderation-specific fields
    /// come from repositories of their own and are filled by the service.
    /// </summary>
    public ModeratedProfile ToModeratedProfile(GeneralUser user)
    {
        if (user == null)
        {
            return null!;
        }

        var result = ToModeratedProfileCore(user);
        FillProfileComputed(result, user);
        return result;
    }

    // The two profile projections share what a map cannot express member by
    // member: an unconditional rating and a privacy-gated birthday.
    private static void FillProfileComputed(UserProfile result, GeneralUser user)
    {
        // Unlike the list-level DTO, the profile page always shows the
        // rating regardless of the owner's visibility setting.
        result.Rating = new Rating { TotalPosts = user.QuantityRating, PostReviewScoreSum = user.QualityRating };
        result.Birthday = user.ShowBirthday && user.BirthdayDate.HasValue
            ? new Birthday
            {
                Day = user.BirthdayDate.Value.Day,
                Month = user.BirthdayDate.Value.Month,
                Year = user.BirthdayDate.Value.Year
            }
            : null;
    }

    /// <summary>
    /// Domain avatar (single source key) to the API picture (3 URLs). Builds
    /// signed imgproxy square thumbnails on the fly (center-crop + resize +
    /// format negotiation by the browser Accept header); the original stays
    /// direct-served - it is already at the right size.
    /// </summary>
    public UserPicture ToUserPicture(AvatarPicture picture)
    {
        if (picture == null || string.IsNullOrEmpty(picture.SourceObjectKey))
        {
            return new UserPicture();
        }

        return new UserPicture
        {
            SmallUrl = _imgproxy.BuildSquareThumbnail(picture.SourceObjectKey, SmallAvatarSize),
            MediumUrl = _imgproxy.BuildSquareThumbnail(picture.SourceObjectKey, MediumAvatarSize),
            OriginalUrl = picture.SourceUrl,
            // Only for the original. The two thumbnails are square center-crops
            // at a size the caller picks, so their dimensions are not news; the
            // original keeps the uploaded aspect ratio and is the one a layout
            // cannot size without being told.
            OriginalWidth = picture.SourceWidth,
            OriginalHeight = picture.SourceHeight,
        };
    }

    [MapProperty(nameof(GeneralUser.UserId), nameof(User.Id))]
    [MapProperty(nameof(GeneralUser.PostReviewsGivenCount), nameof(User.ReviewsGiven))]
    [MapProperty(nameof(GeneralUser.PostReviewsReceivedCount), nameof(User.ReviewsReceived))]
    [MapProperty(nameof(GeneralUser.EndorsementsGivenCount), nameof(User.EndorsementsGiven))]
    [MapProperty(nameof(GeneralUser.EndorsementsReceivedCount), nameof(User.EndorsementsReceived))]
    [MapProperty(nameof(GeneralUser.GameReviewsGivenCount), nameof(User.GameReviewsGiven))]
    [MapProperty(nameof(GeneralUser.GameReviewsReceivedCount), nameof(User.GameReviewsReceived))]
    [MapProperty(nameof(GeneralUser.TopicsAuthoredCount), nameof(User.TopicsAuthored))]
    [MapProperty(nameof(GeneralUser.CommentsAuthoredCount), nameof(User.CommentsAuthored))]
    [MapProperty(nameof(GeneralUser.GlobalChatMessagesCount), nameof(User.GlobalChatMessages))]
    [MapProperty(nameof(GeneralUser.BansReceivedCount), nameof(User.BansReceived))]
    [MapProperty(nameof(GeneralUser.GameDropsCount), nameof(User.GameDrops))]
    [MapProperty(nameof(GeneralUser.PublicationsAuthoredCount), nameof(User.PublicationsAuthored))]
    [MapProperty(nameof(GeneralUser.LikesReceivedCount), nameof(User.LikesReceived))]
    [MapperIgnoreTarget(nameof(User.Rating))]
    // Belongs to the identity of the request, not to the row being projected,
    // so no projection here can fill it. The endpoints that answer about the
    // caller set it after the map; for everybody else it stays absent, which is
    // what tells a reader "this response is not about you".
    [MapperIgnoreTarget(nameof(User.PrivilegeWithheld))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial User ToUserCore(GeneralUser user);

    [MapProperty(nameof(GeneralUser.UserId), nameof(UserProfile.Id))]
    [MapProperty(nameof(GeneralUser.PostReviewsGivenCount), nameof(UserProfile.ReviewsGiven))]
    [MapProperty(nameof(GeneralUser.PostReviewsReceivedCount), nameof(UserProfile.ReviewsReceived))]
    [MapProperty(nameof(GeneralUser.PostReviewsGivenCount), nameof(UserProfile.PostReviewsGiven))]
    [MapProperty(nameof(GeneralUser.PostReviewsReceivedCount), nameof(UserProfile.PostReviewsReceived))]
    [MapProperty(nameof(GeneralUser.EndorsementsGivenCount), nameof(UserProfile.EndorsementsGiven))]
    [MapProperty(nameof(GeneralUser.EndorsementsReceivedCount), nameof(UserProfile.EndorsementsReceived))]
    [MapProperty(nameof(GeneralUser.GameReviewsGivenCount), nameof(UserProfile.GameReviewsGiven))]
    [MapProperty(nameof(GeneralUser.GameReviewsReceivedCount), nameof(UserProfile.GameReviewsReceived))]
    [MapProperty(nameof(GeneralUser.TopicsAuthoredCount), nameof(UserProfile.TopicsAuthored))]
    [MapProperty(nameof(GeneralUser.CommentsAuthoredCount), nameof(UserProfile.CommentsAuthored))]
    [MapProperty(nameof(GeneralUser.GlobalChatMessagesCount), nameof(UserProfile.GlobalChatMessages))]
    [MapProperty(nameof(GeneralUser.BansReceivedCount), nameof(UserProfile.BansReceived))]
    [MapProperty(nameof(GeneralUser.GameDropsCount), nameof(UserProfile.GameDrops))]
    [MapProperty(nameof(GeneralUser.PublicationsAuthoredCount), nameof(UserProfile.PublicationsAuthored))]
    [MapProperty(nameof(GeneralUser.LikesReceivedCount), nameof(UserProfile.LikesReceived))]
    // Composed in the wrapper: the rating is unconditional here and the
    // birthday is privacy-gated.
    [MapperIgnoreTarget(nameof(UserProfile.Rating))]
    [MapperIgnoreTarget(nameof(UserProfile.Birthday))]
    // Filled by the endpoints that fetch them, not by this projection.
    [MapperIgnoreTarget(nameof(UserProfile.UsernameHistory))]
    [MapperIgnoreTarget(nameof(UserProfile.PersonalNote))]
    [MapperIgnoreTarget(nameof(UserProfile.Contacts))]
    [MapperIgnoreTarget(nameof(UserProfile.Info))]
    // A property of the caller's identity; see ToUserCore.
    [MapperIgnoreTarget(nameof(UserProfile.PrivilegeWithheld))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial UserProfile ToUserProfileCore(GeneralUser user);

    [MapProperty(nameof(GeneralUser.UserId), nameof(UserProfile.Id))]
    [MapProperty(nameof(GeneralUser.PostReviewsGivenCount), nameof(UserProfile.ReviewsGiven))]
    [MapProperty(nameof(GeneralUser.PostReviewsReceivedCount), nameof(UserProfile.ReviewsReceived))]
    [MapProperty(nameof(GeneralUser.PostReviewsGivenCount), nameof(UserProfile.PostReviewsGiven))]
    [MapProperty(nameof(GeneralUser.PostReviewsReceivedCount), nameof(UserProfile.PostReviewsReceived))]
    [MapProperty(nameof(GeneralUser.EndorsementsGivenCount), nameof(UserProfile.EndorsementsGiven))]
    [MapProperty(nameof(GeneralUser.EndorsementsReceivedCount), nameof(UserProfile.EndorsementsReceived))]
    [MapProperty(nameof(GeneralUser.GameReviewsGivenCount), nameof(UserProfile.GameReviewsGiven))]
    [MapProperty(nameof(GeneralUser.GameReviewsReceivedCount), nameof(UserProfile.GameReviewsReceived))]
    [MapProperty(nameof(GeneralUser.TopicsAuthoredCount), nameof(UserProfile.TopicsAuthored))]
    [MapProperty(nameof(GeneralUser.CommentsAuthoredCount), nameof(UserProfile.CommentsAuthored))]
    [MapProperty(nameof(GeneralUser.GlobalChatMessagesCount), nameof(UserProfile.GlobalChatMessages))]
    [MapProperty(nameof(GeneralUser.BansReceivedCount), nameof(UserProfile.BansReceived))]
    [MapProperty(nameof(GeneralUser.GameDropsCount), nameof(UserProfile.GameDrops))]
    [MapProperty(nameof(GeneralUser.PublicationsAuthoredCount), nameof(UserProfile.PublicationsAuthored))]
    [MapProperty(nameof(GeneralUser.LikesReceivedCount), nameof(UserProfile.LikesReceived))]
    [MapperIgnoreTarget(nameof(UserProfile.Rating))]
    [MapperIgnoreTarget(nameof(UserProfile.Birthday))]
    [MapperIgnoreTarget(nameof(UserProfile.UsernameHistory))]
    [MapperIgnoreTarget(nameof(UserProfile.PersonalNote))]
    [MapperIgnoreTarget(nameof(UserProfile.Contacts))]
    [MapperIgnoreTarget(nameof(UserProfile.Info))]
    // Filled by ModeratedProfileApiService after the map: each comes from a
    // repository of its own, and half of them depend on the caller's role
    // rather than on the user being read.
    [MapperIgnoreTarget(nameof(ModeratedProfile.IpAddresses))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.LoginHistory))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.LinkedProfiles))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.ModeratorNotes))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.Violations))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.Permissions))]
    // Never filled on this projection at all: a moderated profile is about
    // somebody else, and whether their rank is withheld is theirs to know.
    [MapperIgnoreTarget(nameof(ModeratedProfile.PrivilegeWithheld))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial ModeratedProfile ToModeratedProfileCore(GeneralUser user);

    /// <summary>
    /// Query string to the domain filter. Mapped by name so a filter added
    /// to both sides needs no third edit here; the two members that do not
    /// line up have to be named to be handled: the sort field is sortBy on
    /// the wire and Sort in the domain, and the direction is derived from
    /// SortOrder by CommunityUserApiService, which knows the per-field
    /// default.
    /// </summary>
    [MapProperty(nameof(UsersQuery.SortBy), nameof(DomainUserFilter.Sort))]
    [MapperIgnoreTarget(nameof(DomainUserFilter.SortAscending))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainUserFilter ToUserFilter(UsersQuery query);

    /// <summary>
    /// Domain contact to the API one (field rename: ContactValue -> Value)
    /// </summary>
    [MapProperty(nameof(DomainUserContact.ContactValue), nameof(Contact.Value))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Contact ToContact(DomainUserContact contact);

    /// <summary>
    /// Domain rename record to the API one
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial UsernameHistoryEntry ToUsernameHistoryEntry(DomainUsernameHistory entry);

    /// <summary>
    /// Caller's own note about a user to the response DTO. The subject is
    /// already the profile being answered, so its two members stay behind.
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial PersonalNote ToPersonalNote(UserProfileNote note);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial ModuleStatusCounts ToModuleStatusCounts(DomainModuleStatusCounts counts);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial SubscriberCounts ToSubscriberCounts(SubscribersByCategory counts);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial SubscriberRef ToSubscriberRef(DomainSubscriberInfo info);
}
