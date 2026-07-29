using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Web.API.Features.Community.Achievements;
using DM.Web.API.Shared.Dto;
using IAchievementService = DM.Domain.Community.Features.Achievements.IAchievementService;
using DomainCreateAchievementType = DM.Domain.Community.Features.Achievements.CreateAchievementType;
using DomainUpdateAchievementCategory = DM.Domain.Community.Features.Achievements.UpdateAchievementCategory;
using DomainUpdateAchievementType = DM.Domain.Community.Features.Achievements.UpdateAchievementType;

namespace DM.Web.API.Features.Moderation.Achievements;

/// <inheritdoc />
internal class AchievementCatalogApiService : IAchievementCatalogApiService
{
    private readonly IAchievementService _achievementService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AchievementCatalogApiService(IAchievementService achievementService, IMapper mapper)
    {
        _achievementService = achievementService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Envelope<AchievementCategory>> UpdateCategory(
        Guid id, UpdateAchievementCategoryRequest request)
    {
        var domain = _mapper.Map<DomainUpdateAchievementCategory>(request);
        domain.Id = id;
        var updated = await _achievementService.UpdateCategoryAsync(domain);
        return new Envelope<AchievementCategory>(_mapper.Map<AchievementCategory>(updated));
    }

    /// <inheritdoc />
    public async Task<Envelope<AchievementType>> CreateType(CreateAchievementTypeRequest request)
    {
        var domain = _mapper.Map<DomainCreateAchievementType>(request);
        var created = await _achievementService.CreateTypeAsync(domain);
        return new Envelope<AchievementType>(_mapper.Map<AchievementType>(created));
    }

    /// <inheritdoc />
    public async Task<Envelope<AchievementType>> UpdateType(Guid id, UpdateAchievementTypeRequest request)
    {
        var domain = _mapper.Map<DomainUpdateAchievementType>(request);
        domain.Id = id;
        var updated = await _achievementService.UpdateTypeAsync(domain);
        return new Envelope<AchievementType>(_mapper.Map<AchievementType>(updated));
    }

    /// <inheritdoc />
    public Task DeleteType(Guid id) => _achievementService.DeleteTypeAsync(id);
}
