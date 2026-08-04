using AutoMapper;
using DM.Web.API.Features.Moderation.Bans;
using DomainWarning = DM.Domain.Moderation.Features.Warnings.Warning;
using DomainBan = DM.Domain.Moderation.Features.Warnings.Ban;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <summary>
/// AutoMapper profile for warning and ban DTOs
/// </summary>
internal class WarningMappingProfile : Profile
{
    /// <inheritdoc />
    public WarningMappingProfile()
    {
        CreateMap<DomainWarning, Warning>()
            .ForMember(d => d.Id, s => s.MapFrom(w => w.WarningId))
            .ForMember(d => d.User, s => s.MapFrom(w => w.TargetUser))
            .ForMember(d => d.Moderator, s => s.MapFrom(w => w.Author))
            .ForMember(d => d.EntityId, s => s.MapFrom(w => w.EntityId != System.Guid.Empty ? w.EntityId : (System.Guid?)null))
            .ForMember(d => d.EntityType, s => s.MapFrom(w => w.EntityType.ToString()))
            .ForMember(d => d.Points, s => s.MapFrom(w => w.Points))
            .ForMember(d => d.Reason, s => s.MapFrom(w => w.Text))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(w => w.CreatedUtc))
            .ForMember(d => d.IsActive, s => s.MapFrom(w => !w.IsRemoved));

        CreateMap<DomainBan, Ban>()
            .ForMember(d => d.Id, s => s.MapFrom(b => b.BanId))
            .ForMember(d => d.User, s => s.MapFrom(b => b.TargetUser))
            .ForMember(d => d.Moderator, s => s.MapFrom(b => b.Author))
            .ForMember(d => d.Type, s => s.MapFrom<BanTypeResolver>())
            .ForMember(d => d.StartedUtc, s => s.MapFrom(b => b.StartedUtc))
            .ForMember(d => d.ExpiresUtc, s => s.MapFrom(b => b.EndedUtc))
            .ForMember(d => d.Comment, s => s.MapFrom(b => b.Comment))
            .ForMember(d => d.IsActive, s => s.MapFrom<BanActivityResolver>())
            .ForMember(d => d.IsLifted, s => s.MapFrom(b => b.IsLifted))
            .ForMember(d => d.IsVoluntary, s => s.MapFrom(b => b.IsVoluntary))
            .ForMember(d => d.LiftedUtc, s => s.MapFrom(b => b.LiftedUtc))
            .ForMember(d => d.LiftedByUserId, s => s.MapFrom(b => b.LiftedByUserId))
            .ForMember(d => d.LiftReason, s => s.MapFrom(b => b.LiftReason));

        // Trimmed public views: no reason/comment, no moderator identity,
        // no causation entity refs — anonymous profile visitors only see
        // the aggregate facts
        CreateMap<DomainWarning, PublicWarning>()
            .ForMember(d => d.Points, s => s.MapFrom(w => w.Points))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(w => w.CreatedUtc))
            .ForMember(d => d.IsActive, s => s.MapFrom(w => !w.IsRemoved));

        CreateMap<DomainBan, PublicBan>()
            .ForMember(d => d.Type, s => s.MapFrom<BanTypeResolver>())
            .ForMember(d => d.StartedUtc, s => s.MapFrom(b => b.StartedUtc))
            .ForMember(d => d.ExpiresUtc, s => s.MapFrom(b => b.EndedUtc))
            .ForMember(d => d.IsActive, s => s.MapFrom<BanActivityResolver>());
    }
}
