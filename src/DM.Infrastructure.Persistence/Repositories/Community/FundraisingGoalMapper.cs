using System.Linq;
using DM.Domain.Community.Features.Fundraising;
using Riok.Mapperly.Abstractions;
using DbFundraisingGoal = DM.Infrastructure.Persistence.Entities.Community.FundraisingGoal;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <summary>
/// Compile-time mapper for the fundraising goal. The queryable method is the
/// ProjectTo replacement: Mapperly inlines the projection into the EF Select,
/// and RMG068 (raised to error) breaks the build if it ever cannot.
/// </summary>
[Mapper]
internal static partial class FundraisingGoalMapper
{
    /// <summary>
    /// EF projection to the domain DTO
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial IQueryable<FundraisingGoal> ProjectToFundraisingGoal(
        this IQueryable<DbFundraisingGoal> query);

    /// <summary>
    /// Tracked entity to the domain DTO, for the write path that already
    /// holds the row
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial FundraisingGoal ToFundraisingGoal(this DbFundraisingGoal goal);
}
