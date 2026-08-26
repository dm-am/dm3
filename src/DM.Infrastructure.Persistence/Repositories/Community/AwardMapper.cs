using System;
using System.Linq;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Riok.Mapperly.Abstractions;
using EntityAwardType = DM.Infrastructure.Persistence.Entities.Community.AwardType;
using EntityContestSeries = DM.Infrastructure.Persistence.Entities.Community.ContestSeries;
using EntityUserAward = DM.Infrastructure.Persistence.Entities.Community.UserAward;
using DomainAwardType = DM.Domain.Community.Features.Awards.AwardType;
using DomainContestSeries = DM.Domain.Community.Features.Awards.ContestSeries;
using DomainUserAward = DM.Domain.Community.Features.Awards.UserAward;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <summary>
/// Compile-time mapper for awards. AwardedBy and Note stay in the DB for
/// audit - the domain model has no such members, and the target-required
/// strategy is what allows the narrowing.
/// </summary>
[Mapper]
internal static partial class AwardMapper
{
    public static partial IQueryable<DomainAwardType> ProjectToAwardType(
        this IQueryable<EntityAwardType> query);

    public static partial IQueryable<DomainContestSeries> ProjectToContestSeries(
        this IQueryable<EntityContestSeries> query);

    /// <summary>
    /// EF projection for granted awards. Written out because the user half
    /// goes through the shared projection formula, which Mapperly cannot
    /// inline into a generated queryable.
    /// </summary>
    public static IQueryable<DomainUserAward> ProjectToUserAward(
        this IQueryable<EntityUserAward> query) =>
        query.Select(ExpressionSplicer.Expand<Func<EntityUserAward, DomainUserAward>>(
            a => new DomainUserAward
            {
                Id = a.UserAwardId,
                AwardedUtc = a.AwardedUtc,
                WorkUrl = a.WorkUrl,
                User = GeneralUserProjections.Projection.Splice(a.User),
                Type = new DomainAwardType
                {
                    Id = a.AwardType.AwardTypeId,
                    Code = a.AwardType.Code,
                    Title = a.AwardType.Title,
                    Description = a.AwardType.Description,
                    IconName = a.AwardType.IconName,
                    Tier = a.AwardType.Tier,
                    SortOrder = a.AwardType.SortOrder,
                    IsActive = a.AwardType.IsActive
                },
                ContestSeries = a.ContestSeries == null
                    ? null
                    : new DomainContestSeries
                    {
                        Id = a.ContestSeries.ContestSeriesId,
                        ContestType = a.ContestSeries.ContestType,
                        Number = a.ContestSeries.Number,
                        Year = a.ContestSeries.Year,
                        TopicUrl = a.ContestSeries.TopicUrl,
                        IsActive = a.ContestSeries.IsActive
                    }
            }));

    /// <summary>
    /// Tracked entity to the domain DTO, for the write path that already
    /// holds the row
    /// </summary>
    [MapProperty(nameof(EntityAwardType.AwardTypeId), nameof(DomainAwardType.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial DomainAwardType ToAwardType(this EntityAwardType type);

    [MapProperty(nameof(EntityContestSeries.ContestSeriesId), nameof(DomainContestSeries.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial DomainContestSeries ToContestSeries(this EntityContestSeries series);
}
