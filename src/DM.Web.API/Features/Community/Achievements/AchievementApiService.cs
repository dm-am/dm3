using System.Linq;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;
using IAchievementService = DM.Domain.Community.Features.Achievements.IAchievementService;

namespace DM.Web.API.Features.Community.Achievements;

public class AchievementApiService
{
    private readonly IAchievementService _achievementService;
    private readonly AchievementMapper _mapper;

    public AchievementApiService(IAchievementService achievementService, AchievementMapper mapper)
    {
        _achievementService = achievementService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<AchievementCategory>> GetCategories()
    {
        var categories = await _achievementService.GetCategoriesAsync();
        var items = categories.Select(_mapper.ToCategory);
        return new ListEnvelope<AchievementCategory>(items);
    }

    public async Task<ListEnvelope<AchievementType>> GetTypes()
    {
        var types = await _achievementService.GetTypesAsync();
        var items = types.Select(_mapper.ToType);
        return new ListEnvelope<AchievementType>(items);
    }

    public async Task<ListEnvelope<UserAchievement>> GetUserAchievements(string username)
    {
        var list = await _achievementService.GetUserAchievementsAsync(username);
        var items = list.Select(_mapper.ToUserAchievement);
        return new ListEnvelope<UserAchievement>(items);
    }
}
