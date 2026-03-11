using AutoMapper;
using DM.Domain.Personal.Features.Blacklists;
using DM.Domain.Personal.Features.Notifications;
using DM.Domain.Personal.Features.ProfileNotes;
using DM.Infrastructure.Persistence.Entities.Personal.Notifications;
using DbUserBlacklist = DM.Infrastructure.Persistence.Entities.Account.UserBlacklist;
using DbUserProfileNote = DM.Infrastructure.Persistence.Entities.Account.UserProfileNote;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <summary>
/// AutoMapper profile for Personal repository mappings
/// </summary>
internal class PersonalMappingProfile : Profile
{
    /// <inheritdoc />
    public PersonalMappingProfile()
    {
        // UserBlacklist -> BlacklistEntry
        CreateMap<DbUserBlacklist, BlacklistEntry>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.EntryId))
            .ForMember(d => d.Username, o => o.MapFrom(s => s.BlockedUser.Username))
            .ForMember(d => d.CreatedUtc, o => o.MapFrom(s => s.CreatedUtc));

        // UserProfileNote -> Domain.Core.Personal.UserProfileNote
        CreateMap<DbUserProfileNote, UserProfileNote>()
            .ForMember(d => d.NoteId, o => o.MapFrom(s => s.UserProfileNoteId))
            .ForMember(d => d.SubjectUsername, o => o.MapFrom(s => s.SubjectUser.Username))
            .ForMember(d => d.SubjectUserId, o => o.MapFrom(s => s.SubjectUserId))
            .ForMember(d => d.Text, o => o.MapFrom(s => s.Text))
            .ForMember(d => d.CreatedUtc, o => o.MapFrom(s => s.CreatedUtc))
            .ForMember(d => d.UpdatedUtc, o => o.MapFrom(s => s.UpdatedUtc));

        // Notification -> RealtimeNotification
        CreateMap<Notification, RealtimeNotification>()
            .ForMember(d => d.RecipientIds, s => s.MapFrom(n => n.UsersInterested));
    }
}
