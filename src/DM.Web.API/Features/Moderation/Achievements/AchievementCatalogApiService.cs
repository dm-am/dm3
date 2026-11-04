using System;
using System.Threading.Tasks;
using System.Linq;
using DM.Web.API.Features.Community.Achievements;
using DM.Web.API.Shared.Dto;
using IAchievementService = DM.Domain.Community.Features.Achievements.IAchievementService;

namespace DM.Web.API.Features.Moderation.Achievements;

public class AchievementCatalogApiService
{
    private readonly IAchievementService _achievementService;
    private readonly AchievementMapper _mapper;

    public AchievementCatalogApiService(IAchievementService achievementService, AchievementMapper mapper)
    {
        _achievementService = achievementService;
        _mapper = mapper;
    }

    /// <summary>Inactive included: the public catalog serves active categories only, and the admin page must keep a deactivated one to restore it.</summary>
    public async Task<ListEnvelope<AchievementCategory>> GetCategories()
    {
        var categories = await _achievementService.GetCategoriesAsync(includeInactive: true);
        return new ListEnvelope<AchievementCategory>(
            categories.Select(_mapper.ToCategory));
    }

    public async Task<ListEnvelope<AchievementType>> GetTypes()
    {
        var types = await _achievementService.GetTypesAsync(includeInactive: true);
        return new ListEnvelope<AchievementType>(
            types.Select(_mapper.ToType));
    }

    public async Task<Envelope<AchievementCategory>> UpdateCategory(
        Guid id, UpdateAchievementCategoryRequest request)
    {
        var domain = _mapper.ToUpdateCategory(request);
        domain.Id = id;
        var updated = await _achievementService.UpdateCategoryAsync(domain);
        return new Envelope<AchievementCategory>(_mapper.ToCategory(updated));
    }

    public async Task<Envelope<AchievementType>> CreateType(CreateAchievementTypeRequest request)
    {
        var domain = _mapper.ToCreateType(request);
        var created = await _achievementService.CreateTypeAsync(domain);
        return new Envelope<AchievementType>(_mapper.ToType(created));
    }

    public async Task<Envelope<AchievementType>> UpdateType(Guid id, UpdateAchievementTypeRequest request)
    {
        var domain = _mapper.ToUpdateType(request);
        domain.Id = id;
        var updated = await _achievementService.UpdateTypeAsync(domain);
        return new Envelope<AchievementType>(_mapper.ToType(updated));
    }

    public Task DeleteType(Guid id) => _achievementService.DeleteTypeAsync(id);
}
