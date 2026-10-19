using Riok.Mapperly.Abstractions;
using DomainFundraisingGoal = DM.Domain.Community.Features.Fundraising.FundraisingGoal;
using DomainUpdateFundraisingGoal = DM.Domain.Community.Features.Fundraising.UpdateFundraisingGoal;

namespace DM.Web.API.Features.Community.Fundraising;

/// <summary>
/// Compile-time mapper for the fundraising goal
/// </summary>
[Mapper]
public partial class FundraisingMapper
{
    /// <summary>
    /// Domain goal to the fundraising DTO
    /// </summary>
    public partial Fundraising ToFundraising(DomainFundraisingGoal goal);

    /// <summary>
    /// Update request to the domain command
    /// </summary>
    public partial DomainUpdateFundraisingGoal ToUpdateGoal(UpdateFundraisingRequest request);
}
