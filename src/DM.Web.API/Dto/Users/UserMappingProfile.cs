using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Account.EmailChange;
using DM.Services.Community.BusinessProcesses.Account.PasswordChange;
using DM.Services.Community.BusinessProcesses.Account.PasswordReset;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Community.BusinessProcesses.Users.LoginChange;
using DM.Services.Community.BusinessProcesses.Users.LoginHistory;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Community.BusinessProcesses.Users.Updating;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DtoUserDetails = DM.Services.Community.BusinessProcesses.Users.Reading.UserDetails;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// AutoMapper profile for User entity mappings between Service and API layers
/// </summary>
internal class UserMappingProfile : Profile
{
    /// <inheritdoc />
    public UserMappingProfile()
    {
        CreateMap<UserRole, IEnumerable<UserRole>>()
            .ConvertUsing(userRole => new[] { userRole });

        CreateMap<GeneralUser, UserSummary>()
            .ForMember(d => d.Id, s => s.MapFrom(u => u.UserId))
            .ForMember(d => d.PrimaryRole, s => s.MapFrom(u => u.Role))
            .ForMember(d => d.Rating, s => s.MapFrom(u => CreateRating(u)))
            .ForMember(d => d.Picture, s => s.MapFrom(u => CreateSummaryPicture(u)));

        CreateMap<GeneralUser, User>()
            .IncludeBase<GeneralUser, UserSummary>()
            .ForMember(d => d.Roles, s => s.MapFrom(u => u.Role))
            .ForMember(d => d.Birthday, s => s.MapFrom(u => CreateBirthday(u.BirthdayDate)))
            .ForMember(d => d.Picture, s => s.MapFrom(u => CreateProfilePicture(u)));

        CreateMap<DtoUserDetails, UserDetails>()
            .IncludeBase<GeneralUser, User>()
            .ForMember(d => d.RegistrationDateUtc, s => s.MapFrom(u => u.CreatedUtc))
            .ForMember(d => d.Contacts, s => s.MapFrom(u => MapContacts(u)))
            .ForMember(d => d.Picture, s => s.MapFrom(u => CreateAccountPicture(u)));

        CreateMap<DM.Services.Authentication.Dto.UserSettings, UserSettings>()
            .ForMember(d => d.PagingLimits, s => s.MapFrom(u => u.Paging))
            .ForMember(d => d.IsBirthdayVisible, s => s.Ignore())
            .ForMember(d => d.IsBirthdayYearVisible, s => s.Ignore())
            .ReverseMap()
            .ForMember(d => d.Paging, s => s.MapFrom(u => u.PagingLimits))
            .ForMember(d => d.Id, s => s.Ignore());

        CreateMap<DM.Services.Authentication.Dto.PagingSettings, PagingLimits>().ReverseMap();

        CreateMap<UpdateProfile, UpdateUser>()
            .ForMember(d => d.Login, s => s.Ignore())
            .ForMember(d => d.Settings, s => s.Ignore())
            .ForMember(d => d.Contacts, s => s.MapFrom(src =>
                src.Contacts != null
                    ? src.Contacts.Select((c, i) => new UserContactDto
                    {
                        ContactType = c.Title,
                        ContactValue = c.Value,
                        SortOrder = i
                    }).ToList()
                    : (IReadOnlyCollection<UserContactDto>?)null));

        CreateMap<Registration, UserRegistration>();
        CreateMap<ResetPassword, UserPasswordReset>();
        CreateMap<ChangePassword, UserPasswordChange>();
        CreateMap<ChangeEmail, UserEmailChange>();

        // Login History mappings
        CreateMap<LoginHistoryEntry, LoginHistoryDto>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.LoginHistoryId));

        // Login Change mappings
        CreateMap<CreateLoginChangeRequestDto, CreateLoginChangeRequest>();
        CreateMap<ResolveLoginChangeRequestDto, ResolveLoginChangeRequest>()
            .ForMember(d => d.RequestId, s => s.Ignore());
        CreateMap<LoginChangeRequestEntry, LoginChangeRequestDto>()
            .ForMember(d => d.Id, s => s.MapFrom(e => e.RequestId));

        // Best Post mapping
        CreateMap<DM.Services.Game.BusinessProcesses.Posts.Reading.BestPostResult, BestPost>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.PostId));
    }

    private static UserRating CreateRating(GeneralUser user) => new()
    {
        IsEnabled = !user.RatingDisabled,
        TotalRating = user.QualityRating,
        TotalPosts = user.QuantityRating,
        TotalPostReviewsGiven = user.PostReviewsGivenCount
    };

    private static UserPicture CreateSummaryPicture(GeneralUser user) => new()
    {
        SmallUrl = user.SmallPictureUrl
    };

    private static UserPicture CreateProfilePicture(GeneralUser user) => new()
    {
        SmallUrl = user.SmallPictureUrl,
        MediumUrl = user.MediumPictureUrl
    };

    private static UserPicture CreateAccountPicture(GeneralUser user) => new()
    {
        SmallUrl = user.SmallPictureUrl,
        MediumUrl = user.MediumPictureUrl,
        OriginalUrl = user.OriginalPictureUrl
    };

    private static UserBirthday? CreateBirthday(DateOnly? birthdayDate)
    {
        if (!birthdayDate.HasValue)
            return null;

        return new UserBirthday
        {
            Day = birthdayDate.Value.Day,
            Month = birthdayDate.Value.Month,
            Year = birthdayDate.Value.Year > 1900 ? birthdayDate.Value.Year : null
        };
    }

    private static IEnumerable<UserContact> MapContacts(DtoUserDetails user)
    {
        var contacts = new List<UserContact>();

        // Map from the new contacts system
        if (user.Contacts != null)
        {
            contacts.AddRange(user.Contacts.Select(c => new UserContact
            {
                Title = c.ContactType,
                Value = c.ContactValue
            }));
        }

        // Backward compatibility: include legacy Icq/Skype if present and no new contacts exist
        if (contacts.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(user.Icq))
                contacts.Add(new UserContact { Title = "ICQ", Value = user.Icq });
            if (!string.IsNullOrWhiteSpace(user.Skype))
                contacts.Add(new UserContact { Title = "Skype", Value = user.Skype });
        }

        return contacts;
    }
}
