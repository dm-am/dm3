using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.Achievements;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Achievements;

/// <summary>
/// Публичное чтение каталога достижений и пользовательских записей о
/// получении. Lazy-eval engine стартует на каждый GET по пользователю:
/// типы, пересекшие порог с момента предыдущего вызова, INSERT'ятся
/// и сразу попадают в ответ.
/// </summary>
/// <remarks>
/// Редактирование каталога — в <c>v1/moderation/achievement-categories</c>
/// и <c>v1/moderation/achievement-types</c> (admin only).
/// </remarks>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Achievements")]
public class AchievementController : ControllerBase
{
    private readonly IAchievementService _achievementService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AchievementController(IAchievementService achievementService, IMapper mapper)
    {
        _achievementService = achievementService;
        _mapper = mapper;
    }

    /// <summary>Список категорий достижений (цепочки одной метрики).</summary>
    /// <remarks>Inactive скрыты. SortOrder и описание — SSOT для FE.</remarks>
    /// <response code="200">Список категорий.</response>
    [HttpGet("achievement-categories", Name = nameof(GetAchievementCategories))]
    [ProducesResponseType(typeof(ListEnvelope<AchievementCategory>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAchievementCategories()
    {
        var categories = await _achievementService.GetCategoriesAsync();
        var items = categories.Select(_mapper.Map<AchievementCategory>);
        return Ok(new ListEnvelope<AchievementCategory>(items, null));
    }

    /// <summary>Список тиров достижений (только активные, с категорией-родителем).</summary>
    /// <response code="200">Список тиров.</response>
    [HttpGet("achievement-types", Name = nameof(GetAchievementTypes))]
    [ProducesResponseType(typeof(ListEnvelope<AchievementType>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAchievementTypes()
    {
        var types = await _achievementService.GetTypesAsync();
        var items = types.Select(_mapper.Map<AchievementType>);
        return Ok(new ListEnvelope<AchievementType>(items, null));
    }

    /// <summary>
    /// Достижения пользователя. Lazy-eval: за каждый запрос еще-не-полученные
    /// типы проверяются на пересечение порога и автоматически выдаются.
    /// </summary>
    /// <param name="username">Имя пользователя.</param>
    /// <response code="200">Список достижений (новые сверху).</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpGet("users/{username}/achievements", Name = nameof(GetUserAchievements))]
    [ProducesResponseType(typeof(ListEnvelope<UserAchievement>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAchievements(string username)
    {
        var list = await _achievementService.GetUserAchievementsAsync(username);
        var items = list.Select(_mapper.Map<UserAchievement>);
        return Ok(new ListEnvelope<UserAchievement>(items, null));
    }
}
