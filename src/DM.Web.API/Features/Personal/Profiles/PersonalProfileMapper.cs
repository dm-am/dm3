using System.Linq;
using DM.Domain.Core.Users;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;
using DomainUpdateUser = DM.Domain.Personal.Features.Profiles.UpdateUser;

namespace DM.Web.API.Features.Personal.Profiles;

/// <summary>
/// Compile-time mapper for the account owner's own profile.
/// </summary>
[Mapper]
internal partial class PersonalProfileMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public PersonalProfileMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain user details to the own-profile DTO. Unlike the public
    /// profile, the birthday is always full - visibility for others is
    /// controlled via the Visibility block, not by hiding it from the owner.
    /// </summary>
    public PersonalProfile ToPersonalProfile(UserDetails user)
    {
        if (user == null)
        {
            return null!;
        }

        var result = ToPersonalProfileCore(user);
        result.Rating = new Rating { TotalPosts = user.QuantityRating, PostReviewScoreSum = user.QualityRating };
        result.Birthday = user.BirthdayDate.HasValue
            ? new Birthday
            {
                Day = user.BirthdayDate.Value.Day,
                Month = user.BirthdayDate.Value.Month,
                Year = user.BirthdayDate.Value.Year
            }
            : null;
        result.Contacts = user.Contacts
            .OrderBy(c => c.SortOrder)
            .Select(c => new Contact { ContactType = c.ContactType, Value = c.ContactValue })
            .ToList();
        result.Visibility = new VisibilitySettings
        {
            ShowBirthday = user.ShowBirthday,
            ShowRating = !user.RatingDisabled
        };
        result.Info = string.IsNullOrEmpty(user.Info) ? null : new InfoBbText { Value = user.Info };
        return result;
    }

    /// <summary>
    /// Profile PATCH to the write model. Username belongs to the caller's
    /// identity and Settings to the preferences endpoint - the service sets
    /// the first and never reads the second from this path. Null everywhere
    /// means "not sent": the contact list in particular must stay null when
    /// absent, because an empty list is an instruction to remove them all.
    /// </summary>
    public DomainUpdateUser ToUpdateUser(UpdateProfile profile) => new()
    {
        Status = profile.Status!,
        Name = profile.Name!,
        Location = profile.Location!,
        Info = profile.Info!,
        ShowBirthday = profile.Visibility?.ShowBirthday,
        // Lifted negation on purpose: a visibility block whose flag was not
        // sent folds to null, not to a value.
        RatingDisabled = profile.Visibility == null ? null : !profile.Visibility.ShowRating,
        Contacts = profile.Contacts == null
            ? null!
            : profile.Contacts
                .Select((c, i) => new UserContact
                {
                    ContactType = c.ContactType,
                    ContactValue = c.Value,
                    SortOrder = i
                })
                .ToList(),
        AvatarUploadId = profile.AvatarUploadId
    };

    // Rating, birthday, contacts, visibility and info are composed in the
    // wrapper; the history and the personal note belong to other endpoints.
    [MapProperty(nameof(UserDetails.UserId), nameof(PersonalProfile.Id))]
    [MapProperty(nameof(UserDetails.CreatedUtc), nameof(PersonalProfile.RegisteredUtc))]
    [MapProperty(nameof(UserDetails.PostReviewsGivenCount), nameof(PersonalProfile.ReviewsGiven))]
    [MapProperty(nameof(UserDetails.PostReviewsReceivedCount), nameof(PersonalProfile.ReviewsReceived))]
    [MapProperty(nameof(UserDetails.PostReviewsGivenCount), nameof(PersonalProfile.PostReviewsGiven))]
    [MapProperty(nameof(UserDetails.PostReviewsReceivedCount), nameof(PersonalProfile.PostReviewsReceived))]
    [MapProperty(nameof(UserDetails.EndorsementsGivenCount), nameof(PersonalProfile.EndorsementsGiven))]
    [MapProperty(nameof(UserDetails.EndorsementsReceivedCount), nameof(PersonalProfile.EndorsementsReceived))]
    [MapProperty(nameof(UserDetails.GameReviewsGivenCount), nameof(PersonalProfile.GameReviewsGiven))]
    [MapProperty(nameof(UserDetails.GameReviewsReceivedCount), nameof(PersonalProfile.GameReviewsReceived))]
    [MapProperty(nameof(UserDetails.TopicsAuthoredCount), nameof(PersonalProfile.TopicsAuthored))]
    [MapProperty(nameof(UserDetails.CommentsAuthoredCount), nameof(PersonalProfile.CommentsAuthored))]
    [MapProperty(nameof(UserDetails.GlobalChatMessagesCount), nameof(PersonalProfile.GlobalChatMessages))]
    [MapProperty(nameof(UserDetails.BansReceivedCount), nameof(PersonalProfile.BansReceived))]
    [MapProperty(nameof(UserDetails.GameDropsCount), nameof(PersonalProfile.GameDrops))]
    [MapProperty(nameof(UserDetails.PublicationsAuthoredCount), nameof(PersonalProfile.PublicationsAuthored))]
    [MapProperty(nameof(UserDetails.LikesReceivedCount), nameof(PersonalProfile.LikesReceived))]
    [MapperIgnoreTarget(nameof(PersonalProfile.Rating))]
    [MapperIgnoreTarget(nameof(PersonalProfile.Birthday))]
    [MapperIgnoreTarget(nameof(PersonalProfile.Contacts))]
    [MapperIgnoreTarget(nameof(PersonalProfile.Visibility))]
    [MapperIgnoreTarget(nameof(PersonalProfile.Info))]
    [MapperIgnoreTarget(nameof(PersonalProfile.UsernameHistory))]
    [MapperIgnoreTarget(nameof(PersonalProfile.PersonalNote))]
    // Read off the identity of the request by the API service, not off the row:
    // the profile comes from the database and therefore carries the recorded
    // role, which is exactly why the withheld flag has to come from elsewhere.
    [MapperIgnoreTarget(nameof(PersonalProfile.PrivilegeWithheld))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial PersonalProfile ToPersonalProfileCore(UserDetails user);

    // A narrowing pair (the domain side carries a computed Total); declared
    // so the required-mapping strategy applies to the nested map too.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial ModuleStatusCounts ToModuleStatusCounts(DM.Domain.Core.Dto.ModuleStatusCounts counts);
}
