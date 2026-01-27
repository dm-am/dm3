using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Account.EmailChange;
using DM.Services.Community.BusinessProcesses.Account.PasswordChange;
using DM.Services.Community.BusinessProcesses.Account.PasswordReset;
using DM.Services.Community.BusinessProcesses.Account.Registration;
using DM.Services.Community.BusinessProcesses.Users.Updating;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DtoUserDetails = DM.Services.Community.BusinessProcesses.Users.Reading.UserDetails;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// Mapping profile from Service DTO to API DTO for users
/// </summary>
internal class UserProfile : Profile
{
    /// <inheritdoc />
    public UserProfile()
    {
        CreateMap<UserRole, IEnumerable<UserRole>>()
            .ConvertUsing(userRole => new[] { userRole });

        CreateMap<GeneralUser, User>()
            .ForMember(d => d.Id, s => s.MapFrom(u => u.UserId))
            .ForMember(d => d.Roles, s => s.MapFrom(u => u.Role))
            .ForMember(d => d.OnlineUtc, s => s.MapFrom(u => u.LastActivityUtc))
            .ForMember(d => d.Rating, s => s.MapFrom(u => new Rating
            {
                IsEnabled = !u.RatingDisabled,
                TotalRating = u.QualityRating,
                TotalPosts = u.QuantityRating
            }));

        CreateMap<DtoUserDetails, UserDetails>()
            .IncludeBase<GeneralUser, User>()
            .ForMember(d => d.RegistrationDateUtc, s => s.MapFrom(u => u.CreatedUtc))
            .ForMember(d => d.Contacts, s => s.MapFrom(u => CreateContacts(u.Icq, u.Skype)));
        CreateMap<DM.Services.Authentication.Dto.UserSettings, UserSettings>()
            .ForMember(d => d.PagingLimits, s => s.MapFrom(u => u.Paging))
            .ReverseMap()
            .ForMember(d => d.Paging, s => s.MapFrom(u => u.PagingLimits));
        CreateMap<DM.Services.Authentication.Dto.PagingSettings, PagingLimits>().ReverseMap();
        CreateMap<UserDetails, UpdateUser>();

        CreateMap<Registration, UserRegistration>();
        CreateMap<ResetPassword, UserPasswordReset>();
        CreateMap<ChangePassword, UserPasswordChange>();
        CreateMap<ChangeEmail, UserEmailChange>();
    }

    private static IEnumerable<UserContact> CreateContacts(string icq, string skype)
    {
        var contacts = new List<UserContact>();
        if (!string.IsNullOrWhiteSpace(icq))
        {
            contacts.Add(new UserContact { Title = "ICQ", Value = icq });
        }
        if (!string.IsNullOrWhiteSpace(skype))
        {
            contacts.Add(new UserContact { Title = "Skype", Value = skype });
        }
        return contacts;
    }
}