using System;
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
/// Compile-time mapper for the full user DTO; other mappers compose this one
/// via <c>[UseMapper]</c>. A narrowing projection by design, so only target
/// members are required.
/// </summary>
[Mapper]
public partial class UserMapper
{
    /// <summary>
    /// General user to the list-level user DTO
    /// </summary>
    public User ToUser(GeneralUser user)
    {
        // Domain DTOs hand users over from `= null!` members, and every composed
        // mapper relies on null in, null out.
        if (user == null)
        {
            return null!;
        }

        var result = ToUserCore(user);
        // Null, not a zeroed block: the client renders "rating disabled".
        result.Rating = user.RatingDisabled ? null : RatingOf(user);
        return result;
    }

    /// <summary>
    /// The rating block of a user, whether shown or not being the caller's call
    /// </summary>
    internal static Rating RatingOf(GeneralUser user) =>
        new() { TotalPosts = user.QuantityRating, PostReviewScoreSum = user.QualityRating };

    /// <summary>
    /// The birthday block of a date, null without one
    /// </summary>
    internal static Birthday? BirthdayOf(DateOnly? date) =>
        date is { } day ? new Birthday { Day = day.Day, Month = day.Month, Year = day.Year } : null;

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
    /// The profile of "Аккаунт удален" out of the summed counters of every removed
    /// account: the profile shape of any user, with the name, the ratings and the game
    /// and blog counts filled, no username history, and nothing personal - no status,
    /// no dates, no picture of anybody's, a zero identifier.
    /// </summary>
    public UserProfile ToRemovedAccountProfile(GeneralUser summary)
    {
        var result = ToUserProfile(summary);
        result.UsernameHistory = [];
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
        result.Rating = RatingOf(user);
        result.Birthday = user.ShowBirthday ? BirthdayOf(user.BirthdayDate) : null;
    }

    /// <summary>
    /// Domain avatar (single source) to the API picture (3 URLs): the square
    /// thumbnails the upload stored next to the original are served from beside
    /// its address, and the original itself is already at the right size.
    /// </summary>
    public UserPicture ToUserPicture(AvatarPicture picture)
    {
        if (picture?.SourceUrl is not { Length: > 0 } original)
        {
            return new UserPicture();
        }

        return new UserPicture
        {
            SmallUrl = AvatarThumbnail.AddressOf(original, AvatarThumbnail.SmallSize),
            MediumUrl = AvatarThumbnail.AddressOf(original, AvatarThumbnail.MediumSize),
            OriginalUrl = original,
            // Only for the original: the thumbnails are squares of a known size.
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
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial User ToUserCore(GeneralUser user);

    [IncludeMappingConfiguration(nameof(ToUserCore))]
    // Composed in the wrapper: the birthday is privacy-gated.
    [MapperIgnoreTarget(nameof(UserProfile.Birthday))]
    // Filled by the endpoints that fetch them, not by this projection.
    [MapperIgnoreTarget(nameof(UserProfile.UsernameHistory))]
    [MapperIgnoreTarget(nameof(UserProfile.PersonalNote))]
    [MapperIgnoreTarget(nameof(UserProfile.BlockedByViewer))]
    [MapperIgnoreTarget(nameof(UserProfile.Viewer))]
    [MapperIgnoreTarget(nameof(UserProfile.Contacts))]
    [MapperIgnoreTarget(nameof(UserProfile.Info))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial UserProfile ToUserProfileCore(GeneralUser user);

    [IncludeMappingConfiguration(nameof(ToUserProfileCore))]
    // Filled by ModeratedProfileApiService from repositories of their own.
    [MapperIgnoreTarget(nameof(ModeratedProfile.IpAddresses))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.LoginHistory))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.LinkedProfiles))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.ModeratorNotes))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.Violations))]
    [MapperIgnoreTarget(nameof(ModeratedProfile.Permissions))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial ModeratedProfile ToModeratedProfileCore(GeneralUser user);

    /// <summary>
    /// Query string to the domain filter, by name; the direction comes from
    /// CommunityUserApiService, which knows the per-field default.
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
