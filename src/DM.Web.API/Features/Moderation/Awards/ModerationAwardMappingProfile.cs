using AutoMapper;
using DomainUserAward = DM.Domain.Community.Features.Awards.UserAward;

namespace DM.Web.API.Features.Moderation.Awards;

/// <inheritdoc />
internal class ModerationAwardMappingProfile : Profile
{
    /// <inheritdoc />
    public ModerationAwardMappingProfile()
    {
        // AwardedBy and Note stay out of the API here too — revoking needs the
        // recipient, not the audit trail.
        CreateMap<DomainUserAward, ContestSeriesAward>();
    }
}
