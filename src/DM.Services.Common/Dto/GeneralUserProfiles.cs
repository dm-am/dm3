using System.Linq;
using AutoMapper;
using DM.Services.Authentication.Dto;
using DM.Services.Core.Dto;
using DM.Services.DataAccess.BusinessObjects.Users;
using DbUserSettings = DM.Services.DataAccess.BusinessObjects.Users.Settings.UserSettings;
using DbPagingSettings = DM.Services.DataAccess.BusinessObjects.Users.Settings.PagingSettings;

namespace DM.Services.Common.Dto;

/// <summary>
/// Profile for user mapping
/// </summary>
internal class GeneralUserProfiles : Profile
{
    /// <inheritdoc />
    public GeneralUserProfiles()
    {
        CreateMap<User, GeneralUser>()
            .ForMember(d => d.OriginalPictureUrl, s => s.MapFrom(u =>
                u.AvatarUpload != null ? u.AvatarUpload.FilePath : null))
            .ForMember(d => d.MediumPictureUrl, s => s.MapFrom(u =>
                u.AvatarUpload != null ? (u.AvatarUpload.MediumFilePath ?? u.AvatarUpload.FilePath) : null))
            .ForMember(d => d.SmallPictureUrl, s => s.MapFrom(u =>
                u.AvatarUpload != null ? (u.AvatarUpload.SmallFilePath ?? u.AvatarUpload.FilePath) : null))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(u => u.LastActivityUtc))
            .ForMember(d => d.PostReviewsGivenCount, s => s.Ignore()); // Set separately after mapping
        CreateMap<User, AuthenticatedUser>()
            .ForMember(d => d.AccessRestrictionPolicies, s => s.MapFrom(
                u => u.BansReceived
                    .Select(b => b.AccessRestrictionPolicy)
                    .ToList()));

        CreateMap<DbUserSettings, UserSettings>()
            .ForMember(d => d.Id, s => s.MapFrom(u => u.UserId));
        CreateMap<DbPagingSettings, PagingSettings>();
    }
}