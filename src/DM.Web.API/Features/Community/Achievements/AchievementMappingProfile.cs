using AutoMapper;
using DM.Domain.Community.Features.Achievements;
using ApiAchievementCategory = DM.Web.API.Features.Community.Achievements.AchievementCategory;
using ApiAchievementType = DM.Web.API.Features.Community.Achievements.AchievementType;
using ApiUserAchievement = DM.Web.API.Features.Community.Achievements.UserAchievement;
using DomainAchievementCategory = DM.Domain.Community.Features.Achievements.AchievementCategory;
using DomainAchievementType = DM.Domain.Community.Features.Achievements.AchievementType;
using DomainUserAchievement = DM.Domain.Community.Features.Achievements.UserAchievement;

namespace DM.Web.API.Features.Community.Achievements;

/// <inheritdoc />
public class AchievementMappingProfile : Profile
{
    /// <inheritdoc />
    public AchievementMappingProfile()
    {
        CreateMap<DomainAchievementCategory, ApiAchievementCategory>();
        CreateMap<DomainAchievementType, ApiAchievementType>();
        CreateMap<DomainUserAchievement, ApiUserAchievement>();

        CreateMap<UpdateAchievementCategoryRequest, UpdateAchievementCategory>()
            .ForMember(d => d.Id, opt => opt.Ignore());

        CreateMap<CreateAchievementTypeRequest, CreateAchievementType>();
        CreateMap<UpdateAchievementTypeRequest, UpdateAchievementType>()
            .ForMember(d => d.Id, opt => opt.Ignore());
    }
}
