using System.Linq;
using AutoMapper;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.Profiles;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;

namespace DM.Web.API.Features.Personal.Profiles;

/// <summary>
/// AutoMapper profile for personal profile mappings.
/// </summary>
internal class PersonalProfileMappingProfile : Profile
{
    private const int NewbieThreshold = 100;

    public PersonalProfileMappingProfile()
    {
        // UserDetails (domain) -> PersonalProfile (API)
        CreateMap<UserDetails, PersonalProfile>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.UserId))
            .ForMember(d => d.IsNewbie, o => o.MapFrom(s => s.QuantityRating < NewbieThreshold))
            .ForMember(d => d.Rating, o => o.MapFrom(s => new Rating
            {
                TotalPosts = s.QuantityRating,
                PostReviewScoreSum = s.QualityRating
            }))
            .ForMember(d => d.Picture, o => o.MapFrom(s => new UserPicture
            {
                SmallUrl = s.SmallPictureUrl,
                MediumUrl = s.MediumPictureUrl,
                OriginalUrl = s.OriginalPictureUrl
            }))
            .ForMember(d => d.Birthday, o => o.MapFrom(s => s.BirthdayDate.HasValue
                ? new Birthday
                {
                    Day = s.BirthdayDate.Value.Day,
                    Month = s.BirthdayDate.Value.Month,
                    Year = s.BirthdayDate.Value.Year
                }
                : null))
            .ForMember(d => d.Contacts, o => o.MapFrom(s => s.Contacts
                .OrderBy(c => c.SortOrder)
                .Select(c => new Contact
                {
                    ContactType = c.ContactType,
                    Value = c.ContactValue
                })))
            .ForMember(d => d.PostReviewsGiven, o => o.MapFrom(s => s.PostReviewsGivenCount))
            .ForMember(d => d.PostReviewsReceived, o => o.MapFrom(s => s.PostReviewsReceivedCount))
            .ForMember(d => d.RegisteredAtUtc, o => o.MapFrom(s => s.CreatedUtc))
            .ForMember(d => d.Visibility, o => o.MapFrom(s => new VisibilitySettings
            {
                ShowBirthday = s.ShowBirthday,
                ShowRating = !s.RatingDisabled
            }))
            .ForMember(d => d.Info, o => o.MapFrom(s => string.IsNullOrEmpty(s.Info)
                ? null
                : new InfoBbText { Value = s.Info }))
            .ForMember(d => d.UsernameHistory, o => o.Ignore())
            .ForMember(d => d.FeaturedPost, o => o.Ignore());

        // UpdateProfile (API) -> UpdateUser (domain)
        CreateMap<UpdateProfile, UpdateUser>()
            .ForMember(d => d.Username, o => o.Ignore())
            .ForMember(d => d.ShowBirthday, o => o.MapFrom(s =>
                s.Visibility != null && s.Visibility.ShowBirthday.HasValue
                    ? s.Visibility.ShowBirthday.Value
                    : (bool?)null))
            .ForMember(d => d.RatingDisabled, o => o.MapFrom(s =>
                s.Visibility != null && s.Visibility.ShowRating.HasValue
                    ? !s.Visibility.ShowRating.Value
                    : (bool?)null))
            .ForMember(d => d.Contacts, o => o.MapFrom(s => s.Contacts != null
                ? s.Contacts.Select((c, i) => new UserContact
                {
                    ContactType = c.ContactType,
                    ContactValue = c.Value,
                    SortOrder = i
                }).ToList()
                : null))
            .ForMember(d => d.AvatarUploadId, o => o.MapFrom(s => s.AvatarUploadId))
            .ForMember(d => d.Settings, o => o.Ignore());
    }
}
