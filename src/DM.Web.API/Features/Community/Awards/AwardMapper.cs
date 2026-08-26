using Riok.Mapperly.Abstractions;
using ApiAwardType = DM.Web.API.Features.Community.Awards.AwardType;
using ApiContestSeries = DM.Web.API.Features.Community.Awards.ContestSeries;
using ApiUserAward = DM.Web.API.Features.Community.Awards.UserAward;
using DomainAwardType = DM.Domain.Community.Features.Awards.AwardType;
using DomainContestSeries = DM.Domain.Community.Features.Awards.ContestSeries;
using DomainCreateAwardType = DM.Domain.Community.Features.Awards.CreateAwardType;
using DomainCreateContestSeries = DM.Domain.Community.Features.Awards.CreateContestSeries;
using DomainUpdateAwardType = DM.Domain.Community.Features.Awards.UpdateAwardType;
using DomainUpdateContestSeries = DM.Domain.Community.Features.Awards.UpdateContestSeries;
using DomainUserAward = DM.Domain.Community.Features.Awards.UserAward;

namespace DM.Web.API.Features.Community.Awards;

/// <summary>
/// Compile-time mapper for awards
/// </summary>
[Mapper]
internal partial class AwardMapper
{
    /// <summary>
    /// Domain award type to its response DTO
    /// </summary>
    public partial ApiAwardType ToAwardType(DomainAwardType type);

    /// <summary>
    /// Domain contest series to its response DTO
    /// </summary>
    public partial ApiContestSeries ToContestSeries(DomainContestSeries series);

    /// <summary>
    /// Domain user award to its response DTO. AwardedBy and Note stay out of
    /// the public API - the audit trail lives in the DB.
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ApiUserAward ToUserAward(DomainUserAward award);

    /// <summary>
    /// Create-type request to the domain command
    /// </summary>
    public partial DomainCreateAwardType ToCreateAwardType(CreateAwardTypeRequest request);

    /// <summary>
    /// Update-type request to the domain command. Id comes from the route,
    /// not the body.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdateAwardType.Id))]
    public partial DomainUpdateAwardType ToUpdateAwardType(UpdateAwardTypeRequest request);

    /// <summary>
    /// Create-series request to the domain command
    /// </summary>
    public partial DomainCreateContestSeries ToCreateContestSeries(CreateContestSeriesRequest request);

    /// <summary>
    /// Update-series request to the domain command. Id comes from the route,
    /// not the body.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdateContestSeries.Id))]
    public partial DomainUpdateContestSeries ToUpdateContestSeries(UpdateContestSeriesRequest request);
}
