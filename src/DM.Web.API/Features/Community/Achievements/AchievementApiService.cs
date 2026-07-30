using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Web.API.Shared.Dto;
using IAchievementService = DM.Domain.Community.Features.Achievements.IAchievementService;

namespace DM.Web.API.Features.Community.Achievements;

/// <inheritdoc />
internal class AchievementApiService : IAchievementApiService
{
    private readonly IAchievementService _achievementService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AchievementApiService(IAchievementService achievementService, IMapper mapper)
    {
        _achievementService = achievementService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<AchievementCategory>> GetCategories()
    {
        var categories = await _achievementService.GetCategoriesAsync();
        var items = categories.Select(_mapper.Map<AchievementCategory>);
        return new ListEnvelope<AchievementCategory>(items, null);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<AchievementType>> GetTypes()
    {
        var types = await _achievementService.GetTypesAsync();
        var items = types.Select(_mapper.Map<AchievementType>);
        return new ListEnvelope<AchievementType>(items, null);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<UserAchievement>> GetUserAchievements(string username)
    {
        var list = await _achievementService.GetUserAchievementsAsync(username);
        var items = list.Select(_mapper.Map<UserAchievement>);
        return new ListEnvelope<UserAchievement>(items, null);
    }
}
