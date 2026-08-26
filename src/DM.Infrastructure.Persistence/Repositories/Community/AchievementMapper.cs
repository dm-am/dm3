using System;
using System.Linq;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using Riok.Mapperly.Abstractions;
using EntityAchievementCategory = DM.Infrastructure.Persistence.Entities.Community.AchievementCategory;
using EntityAchievementType = DM.Infrastructure.Persistence.Entities.Community.AchievementType;
using EntityUserAchievement = DM.Infrastructure.Persistence.Entities.Community.UserAchievement;
using DomainAchievementCategory = DM.Domain.Community.Features.Achievements.AchievementCategory;
using DomainAchievementType = DM.Domain.Community.Features.Achievements.AchievementType;
using DomainUserAchievement = DM.Domain.Community.Features.Achievements.UserAchievement;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <summary>
/// Compile-time mapper for achievements. The queryable methods are the
/// ProjectTo replacement: Mapperly inlines the projections into the EF
/// Select, and RMG068 (raised to error) breaks the build if it ever cannot.
/// The user-carrying earning row is projected by hand instead, through the
/// shared <see cref="GeneralUserProjections"/> formula.
/// </summary>
[Mapper]
internal static partial class AchievementMapper
{
    /// <summary>
    /// EF projection to the domain category. The member configuration lives
    /// on <see cref="ToCategory"/> - a queryable projection inlines the
    /// object mapping of its element type.
    /// </summary>
    public static partial IQueryable<DomainAchievementCategory> ProjectToCategory(
        this IQueryable<EntityAchievementCategory> query);

    /// <summary>
    /// EF projection to the domain tier, category inlined
    /// </summary>
    public static partial IQueryable<DomainAchievementType> ProjectToType(
        this IQueryable<EntityAchievementType> query);

    /// <summary>
    /// EF projection for a user's earnings. Written out because the user half
    /// goes through the shared projection formula, which Mapperly cannot
    /// inline into a generated queryable.
    /// </summary>
    public static IQueryable<DomainUserAchievement> ProjectToUserAchievement(
        this IQueryable<EntityUserAchievement> query) =>
        query.Select(ExpressionSplicer.Expand<Func<EntityUserAchievement, DomainUserAchievement>>(
            a => new DomainUserAchievement
            {
                Id = a.UserAchievementId,
                EarnedUtc = a.EarnedUtc,
                User = GeneralUserProjections.Projection.Splice(a.User),
                Type = new DomainAchievementType
                {
                    Id = a.AchievementType.AchievementTypeId,
                    Code = a.AchievementType.Code,
                    Title = a.AchievementType.Title,
                    Threshold = a.AchievementType.Threshold,
                    Tier = a.AchievementType.Tier,
                    Category = new DomainAchievementCategory
                    {
                        Id = a.AchievementType.Category.AchievementCategoryId,
                        Code = a.AchievementType.Category.Code,
                        Title = a.AchievementType.Category.Title,
                        Description = a.AchievementType.Category.Description,
                        IconName = a.AchievementType.Category.IconName,
                        Metric = a.AchievementType.Category.Metric,
                        SortOrder = a.AchievementType.Category.SortOrder,
                        IsActive = a.AchievementType.Category.IsActive
                    }
                }
            }));

    /// <summary>
    /// Tracked entity to the domain category, for the write path that
    /// already holds the row
    /// </summary>
    [MapProperty(nameof(EntityAchievementCategory.AchievementCategoryId), nameof(DomainAchievementCategory.Id))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public static partial DomainAchievementCategory ToCategory(this EntityAchievementCategory category);

    [MapProperty(nameof(EntityAchievementType.AchievementTypeId), nameof(DomainAchievementType.Id))]
    [MapProperty(nameof(EntityAchievementType.Category), nameof(DomainAchievementType.Category))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private static partial DomainAchievementType ToType(EntityAchievementType type);
}
