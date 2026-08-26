using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Moderation.Features.Warnings;
using DM.Web.API.Shared.Dto;
using ApiBan = DM.Web.API.Features.Moderation.Bans.Ban;
using DomainViolator = DM.Domain.Moderation.Features.Warnings.Violator;

namespace DM.Web.API.Features.Moderation.Warnings;

/// <inheritdoc />
internal class ViolatorApiService : IViolatorApiService
{
    private readonly IWarningService _warningService;
    private readonly WarningMapper _mapper;

    /// <inheritdoc />
    public ViolatorApiService(IWarningService warningService, WarningMapper mapper)
    {
        _warningService = warningService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Violator>> GetViolators(string banState)
    {
        var violators = await _warningService.GetViolators(ParseFilter(banState));
        return new ListEnvelope<Violator>(violators.Select(Map).ToList());
    }

    private Violator Map(DomainViolator violator) => new()
    {
        User = UserRefMappers.ToUserRef(violator.User),
        Points = violator.Points,
        LastWarningUtc = violator.LastWarningUtc,
        ActiveBan = violator.ActiveBan != null ? _mapper.ToBan(violator.ActiveBan) : null
    };

    private static ViolatorsFilter ParseFilter(string filter) => filter switch
    {
        "banned" => ViolatorsFilter.Banned,
        "points-only" => ViolatorsFilter.PointsOnly,
        // The controller query model restricts the value to all|banned|points-only
        _ => ViolatorsFilter.All
    };
}
