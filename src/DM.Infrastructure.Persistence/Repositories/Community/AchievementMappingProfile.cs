using AutoMapper;
using EntityAchievementCategory = DM.Infrastructure.Persistence.Entities.Community.AchievementCategory;
using EntityAchievementType = DM.Infrastructure.Persistence.Entities.Community.AchievementType;
using EntityUserAchievement = DM.Infrastructure.Persistence.Entities.Community.UserAchievement;
using DomainAchievementCategory = DM.Domain.Community.Features.Achievements.AchievementCategory;
using DomainAchievementType = DM.Domain.Community.Features.Achievements.AchievementType;
using DomainUserAchievement = DM.Domain.Community.Features.Achievements.UserAchievement;

namespace DM.Infrastructure.Persistence.Repositories.Community;

internal class AchievementMappingProfile : Profile
{
    public AchievementMappingProfile()
    {
        CreateMap<EntityAchievementCategory, DomainAchievementCategory>()
            .ForMember(d => d.Id, s => s.MapFrom(c => c.AchievementCategoryId));

        CreateMap<EntityAchievementType, DomainAchievementType>()
            .ForMember(d => d.Id, s => s.MapFrom(t => t.AchievementTypeId))
            .ForMember(d => d.Category, s => s.MapFrom(t => t.Category));

        CreateMap<EntityUserAchievement, DomainUserAchievement>()
            .ForMember(d => d.Id, s => s.MapFrom(a => a.UserAchievementId))
            .ForMember(d => d.Type, s => s.MapFrom(a => a.AchievementType))
            .ForMember(d => d.User, s => s.MapFrom(a => a.User));
    }
}
