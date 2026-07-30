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
using EntityUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;

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
            // Avatar projection — SSOT via AvatarProjections.From; no more
            // ternary duplicates in every repository.
            .ForMember(d => d.Picture, s => s.MapFrom(u => AvatarProjections.From(u.AvatarUpload)))
            .ForMember(d => d.LastActivityUtc, s => s.MapFrom(u => u.LastActivityUtc))
            .ForMember(d => d.RegisteredUtc, s => s.MapFrom(u => u.CreatedUtc))
            .ForMember(d => d.UsernameHistory, s => s.Ignore()) // Set separately after mapping (OrderBy not translatable in ProjectTo)
            .ForMember(d => d.PostReviewsGivenCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.PostReviewsReceivedCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.EndorsementsGivenCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.EndorsementsReceivedCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.TopicsAuthoredCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.CommentsAuthoredCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.GlobalChatMessagesCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.BansReceivedCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.GameDropsCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.PublicationsAuthoredCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.LikesReceivedCount, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.GamesHosting, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.GamesHostingByStatus, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.GamesPlaying, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.GamesPlayingByStatus, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.BlogsHosting, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.BlogsHostingByStatus, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.SubscribersByCategory, s => s.Ignore()) // Set separately after mapping
            .ForMember(d => d.Subscribers, s => s.Ignore()); // Set separately after mapping (richer SubscriberInfo list)

        // UsernameHistory entity -> UsernameHistoryEntry domain
        CreateMap<EntityUsernameHistory, UsernameHistoryEntry>()
            .ForMember(d => d.UsernameHistoryId, s => s.MapFrom(h => h.UsernameHistoryId))
            .ForMember(d => d.OldUsername, s => s.MapFrom(h => h.OldUsername))
            .ForMember(d => d.NewUsername, s => s.MapFrom(h => h.NewUsername))
            .ForMember(d => d.ChangedUtc, s => s.MapFrom(h => h.ChangedUtc))
            .ForMember(d => d.ApprovedByUsername, s => s.MapFrom(h => h.ApprovedBy != null ? h.ApprovedBy.Username : null));

        // User -> UserDetails (includes contacts)
        CreateMap<User, UserDetails>()
            .IncludeBase<User, GeneralUser>()
            .ForMember(d => d.Contacts, s => s.MapFrom(u =>
                u.Contacts.OrderBy(c => c.SortOrder).Select(c => new CoreUserContact
                {
                    ContactType = c.ContactType,
                    ContactValue = c.ContactValue,
                    SortOrder = c.SortOrder
                })))
            .ForMember(d => d.Settings, s => s.Ignore()); // Set separately after mapping

        CreateMap<EntityUserContact, CoreUserContact>();

        // The ban window travels with the restriction: whether a ban is in force
        // is a question about time, and the answer belongs to whoever holds a
        // clock, not to the projection. Removed bans are already excluded by the
        // global soft-delete filter.
        CreateMap<User, AuthenticatedUser>()
            .IncludeBase<User, GeneralUser>()
            .ForMember(d => d.AccessRestrictions, s => s.MapFrom(
                u => u.BansReceived
                    .Select(b => new AccessRestriction(b.AccessRestrictionPolicy, b.StartedUtc, b.EndedUtc))
                    .ToList()));

        CreateMap<DbUserSettings, UserSettings>()
            .ForMember(d => d.Id, s => s.MapFrom(u => u.UserId));
        CreateMap<DbPagingSettings, PagingSettings>();
    }
}
