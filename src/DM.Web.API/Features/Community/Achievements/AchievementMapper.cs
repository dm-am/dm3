using Riok.Mapperly.Abstractions;
using ApiAchievementCategory = DM.Web.API.Features.Community.Achievements.AchievementCategory;
using ApiAchievementType = DM.Web.API.Features.Community.Achievements.AchievementType;
using ApiUserAchievement = DM.Web.API.Features.Community.Achievements.UserAchievement;
using DomainAchievementCategory = DM.Domain.Community.Features.Achievements.AchievementCategory;
using DomainAchievementType = DM.Domain.Community.Features.Achievements.AchievementType;
using DomainCreateAchievementType = DM.Domain.Community.Features.Achievements.CreateAchievementType;
using DomainUpdateAchievementCategory = DM.Domain.Community.Features.Achievements.UpdateAchievementCategory;
using DomainUpdateAchievementType = DM.Domain.Community.Features.Achievements.UpdateAchievementType;
using DomainUserAchievement = DM.Domain.Community.Features.Achievements.UserAchievement;

namespace DM.Web.API.Features.Community.Achievements;

/// <summary>
/// Compile-time mapper for achievements
/// </summary>
[Mapper]
internal partial class AchievementMapper
{
    /// <summary>
    /// Domain category to its response DTO
    /// </summary>
    public partial ApiAchievementCategory ToCategory(DomainAchievementCategory category);

    /// <summary>
    /// Domain achievement type to its response DTO
    /// </summary>
    public partial ApiAchievementType ToType(DomainAchievementType type);

    /// <summary>
    /// Domain user achievement to its response DTO. The user stays out: the
    /// list is always fetched for a known profile.
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial ApiUserAchievement ToUserAchievement(DomainUserAchievement achievement);

    /// <summary>
    /// Update-category request to the domain command. Id comes from the
    /// route, not the body.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdateAchievementCategory.Id))]
    public partial DomainUpdateAchievementCategory ToUpdateCategory(UpdateAchievementCategoryRequest request);

    /// <summary>
    /// Create-type request to the domain command
    /// </summary>
    public partial DomainCreateAchievementType ToCreateType(CreateAchievementTypeRequest request);

    /// <summary>
    /// Update-type request to the domain command. Id comes from the route,
    /// not the body.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdateAchievementType.Id))]
    public partial DomainUpdateAchievementType ToUpdateType(UpdateAchievementTypeRequest request);
}
