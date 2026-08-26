using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using DomainUserAward = DM.Domain.Community.Features.Awards.UserAward;

namespace DM.Web.API.Features.Moderation.Awards;

/// <summary>
/// Compile-time mapper for the moderation award catalog
/// </summary>
[Mapper]
[UseStaticMapper(typeof(UserRefMappers))]
internal partial class ModerationAwardMapper
{
    /// <summary>
    /// Domain user award to the catalog row. AwardedBy and Note stay out of
    /// the API here too - revoking needs the recipient, not the audit trail.
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ContestSeriesAward ToContestSeriesAward(DomainUserAward award);
}
