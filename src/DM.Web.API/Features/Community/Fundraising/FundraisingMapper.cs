using Riok.Mapperly.Abstractions;
using DomainFundraisingGoal = DM.Domain.Community.Features.Fundraising.FundraisingGoal;

namespace DM.Web.API.Features.Community.Fundraising;

/// <summary>
/// Compile-time mapper for the fundraising goal
/// </summary>
[Mapper]
internal partial class FundraisingMapper
{
    /// <summary>
    /// Domain goal to the fundraising DTO
    /// </summary>
    public partial Fundraising ToFundraising(DomainFundraisingGoal goal);
}
