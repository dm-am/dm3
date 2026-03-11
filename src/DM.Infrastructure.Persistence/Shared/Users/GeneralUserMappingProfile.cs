using System.Linq;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Users;
using DM.Infrastructure.Persistence.Entities.Account;
using DbUserSettings = DM.Infrastructure.Persistence.Entities.Account.Settings.UserSettings;
using DbPagingSettings = DM.Infrastructure.Persistence.Entities.Account.Settings.PagingSettings;
using CoreUserContact = DM.Domain.Core.Users.UserContact;
using EntityUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;

namespace DM.Infrastructure.Persistence.Shared.Users;

/// <summary>
/// Profile for user mapping
/// </summary>
internal class GeneralUserMappingProfile : Profile
{
    /// <inheritdoc />
    public GeneralUserMappingProfile()
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

        // User -> UserDetails (includes contacts)
        CreateMap<User, UserDetails>()
            .IncludeBase<User, GeneralUser>()
            .ForMember(d => d.Contacts, s => s.MapFrom(u =>
                u.Contacts.OrderBy(c => c.SortOrder).Select(c => new CoreUserContact
                {
                    ContactType = c.ContactType,
                    ContactValue = c.ContactValue,
                    SortOrder = c.SortOrder
                })));

        CreateMap<EntityUserContact, CoreUserContact>();

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
