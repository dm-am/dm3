using AutoMapper;
using DbWarning = DM.Services.DataAccess.BusinessObjects.Administration.Warning;
using DbBan = DM.Services.DataAccess.BusinessObjects.Administration.Ban;

namespace DM.Web.API.Dto.Moderation;

/// <summary>
/// AutoMapper profile for warning and ban DTOs
/// </summary>
internal class WarningProfile : Profile
{
    /// <inheritdoc />
    public WarningProfile()
    {
        CreateMap<DbWarning, Warning>()
            .ForMember(d => d.Id, s => s.MapFrom(w => w.WarningId))
            .ForMember(d => d.User, s => s.MapFrom(w => w.User))
            .ForMember(d => d.Moderator, s => s.MapFrom(w => w.Moderator))
            .ForMember(d => d.EntityId, s => s.MapFrom(w => w.EntityId != System.Guid.Empty ? w.EntityId : (System.Guid?)null))
            .ForMember(d => d.EntityType, s => s.Ignore()) // EntityType not stored in DB
            .ForMember(d => d.Points, s => s.MapFrom(w => w.Points))
            .ForMember(d => d.Reason, s => s.MapFrom(w => w.Text))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(w => w.CreatedUtc))
            .ForMember(d => d.IsActive, s => s.MapFrom(w => !w.IsRemoved));

        CreateMap<DbBan, Ban>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.BanId))
            .ForMember(d => d.User, s => s.MapFrom(b => b.User))
            .ForMember(d => d.Moderator, s => s.MapFrom(b => b.Moderator))
            .ForMember(d => d.Type, s => s.MapFrom(b => MapBanType(b)))
            .ForMember(d => d.StartedUtc, s => s.MapFrom(b => b.StartedUtc))
            .ForMember(d => d.ExpiresUtc, s => s.MapFrom(b => b.EndedUtc))
            .ForMember(d => d.Comment, s => s.MapFrom(b => b.Comment))
            .ForMember(d => d.IsActive, s => s.MapFrom(b => !b.IsRemoved && b.EndedUtc > System.DateTimeOffset.UtcNow))
            .ForMember(d => d.IsVoluntary, s => s.MapFrom(b => b.IsVoluntary))
            .ForMember(d => d.LiftedUtc, s => s.Ignore()) // Not in current schema
            .ForMember(d => d.LiftedBy, s => s.Ignore()); // Not in current schema
    }

    private static BanType MapBanType(DbBan ban)
    {
        if (ban.IsVoluntary)
        {
            return BanType.Voluntary;
        }

        // Check if it's a permanent ban (100+ years into the future)
        if (ban.EndedUtc > System.DateTimeOffset.UtcNow.AddYears(50))
        {
            return BanType.Permanent;
        }

        return BanType.Temporary;
    }
}
