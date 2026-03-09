using AutoMapper;
using DM.Domain.Moderation.Features.ProfileNotes;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Features.Warnings;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;
using DbTicket = DM.Infrastructure.Persistence.Entities.Moderation.Ticket;
using DbModeratedProfileNote = DM.Infrastructure.Persistence.Entities.Account.ModeratedProfileNote;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <summary>
/// AutoMapper profile for Moderation domain DTOs
/// </summary>
internal class ModerationMappingProfile : Profile
{
    /// <inheritdoc />
    public ModerationMappingProfile()
    {
        // Warning mappings
        CreateMap<DbWarning, Warning>()
            .ForMember(d => d.TargetUser, s => s.MapFrom(w => w.TargetUser))
            .ForMember(d => d.Author, s => s.MapFrom(w => w.Author));

        // Ban mappings
        CreateMap<DbBan, Ban>()
            .ForMember(d => d.TargetUser, s => s.MapFrom(b => b.TargetUser))
            .ForMember(d => d.Author, s => s.MapFrom(b => b.Author));

        // Ticket mappings
        CreateMap<DbTicket, Ticket>()
            .ForMember(d => d.ReporterUsername, s => s.MapFrom(t => t.Author.Username))
            .ForMember(d => d.TargetUsername, s => s.MapFrom(t => t.Target.Username))
            .ForMember(d => d.AssignedModeratorUsername, s => s.MapFrom(t =>
                t.AssignedModerator != null ? t.AssignedModerator.Username : null))
            .ForMember(d => d.HasWarning, s => s.MapFrom(t => t.WarningId.HasValue))
            .ForMember(d => d.HasBan, s => s.MapFrom(t => t.BanId.HasValue));

        // ModeratedProfileNote mappings
        CreateMap<DbModeratedProfileNote, ModeratedProfileNote>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.ModeratedProfileNoteId))
            .ForMember(d => d.User, o => o.MapFrom(s => s.User))
            .ForMember(d => d.Author, o => o.MapFrom(s => s.Author))
            .ForMember(d => d.Text, o => o.MapFrom(s => s.Text))
            .ForMember(d => d.CreatedUtc, o => o.MapFrom(s => s.CreatedUtc))
            .ForMember(d => d.UpdatedUtc, o => o.MapFrom(s => s.UpdatedUtc));
    }
}
