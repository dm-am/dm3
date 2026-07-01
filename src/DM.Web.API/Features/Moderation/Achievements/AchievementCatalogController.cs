using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Achievements;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IAchievementService = DM.Domain.Community.Features.Achievements.IAchievementService;
using DomainCreateAchievementType = DM.Domain.Community.Features.Achievements.CreateAchievementType;
using DomainUpdateAchievementCategory = DM.Domain.Community.Features.Achievements.UpdateAchievementCategory;
using DomainUpdateAchievementType = DM.Domain.Community.Features.Achievements.UpdateAchievementType;

namespace DM.Web.API.Features.Moderation.Achievements;

/// <summary>
/// Управление каталогом достижений (категории + тиры).
/// </summary>
/// <remarks>
/// Категории — только PATCH, создания/удаления нет (каталог статичен и
/// привязан к серверным метрикам через <c>AchievementCategory.Metric</c>).
/// Тиры — POST/PATCH/DELETE: SeniorModerator может добавить/убрать тир
/// или подкрутить порог; категория-родитель указывается через FK.
/// Все ручки требуют SeniorModerator или выше.
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Achievement Catalog")]
[RequireRole(UserRole.SeniorModerator)]
public class AchievementCatalogController : ControllerBase
{
    private readonly IAchievementService _achievementService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AchievementCatalogController(IAchievementService achievementService, IMapper mapper)
    {
        _achievementService = achievementService;
        _mapper = mapper;
    }

    /// <summary>
    /// Частично обновить категорию достижений (Title/Description/IconName/SortOrder/IsActive).
    /// </summary>
    /// <param name="id">Идентификатор категории.</param>
    /// <param name="request">Поля для обновления (null = не трогать).</param>
    /// <response code="200">Обновлено.</response>
    /// <response code="400">Неизвестная иконка или невалидные данные.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Категория не найдена.</response>
    [HttpPatch("achievement-categories/{id:guid}", Name = nameof(UpdateAchievementCategory))]
    [ProducesResponseType(typeof(Envelope<AchievementCategory>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAchievementCategory(Guid id, [FromBody] UpdateAchievementCategoryRequest request)
    {
        var domain = _mapper.Map<DomainUpdateAchievementCategory>(request);
        domain.Id = id;
        var updated = await _achievementService.UpdateCategoryAsync(domain);
        return Ok(new Envelope<AchievementCategory>(_mapper.Map<AchievementCategory>(updated)));
    }

    /// <summary>Создать новый тир в существующей категории.</summary>
    /// <response code="201">Создан.</response>
    /// <response code="400">Неизвестная категория или невалидные данные.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="409">Code уже занят.</response>
    [HttpPost("achievement-types", Name = nameof(CreateAchievementType))]
    [ProducesResponseType(typeof(Envelope<AchievementType>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAchievementType([FromBody] CreateAchievementTypeRequest request)
    {
        var domain = _mapper.Map<DomainCreateAchievementType>(request);
        var created = await _achievementService.CreateTypeAsync(domain);
        var api = _mapper.Map<AchievementType>(created);
        return CreatedAtRoute(
            nameof(AchievementController.GetAchievementTypes),
            null,
            new Envelope<AchievementType>(api));
    }

    /// <summary>Частично обновить тир (Title / Threshold / Tier).</summary>
    /// <param name="id">Идентификатор тира.</param>
    /// <param name="request">Поля для обновления.</param>
    /// <response code="200">Обновлено.</response>
    /// <response code="400">Невалидные данные.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Тир не найден.</response>
    [HttpPatch("achievement-types/{id:guid}", Name = nameof(UpdateAchievementType))]
    [ProducesResponseType(typeof(Envelope<AchievementType>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAchievementType(Guid id, [FromBody] UpdateAchievementTypeRequest request)
    {
        var domain = _mapper.Map<DomainUpdateAchievementType>(request);
        domain.Id = id;
        var updated = await _achievementService.UpdateTypeAsync(domain);
        return Ok(new Envelope<AchievementType>(_mapper.Map<AchievementType>(updated)));
    }

    /// <summary>Удалить тир из каталога. Уже выданные UserAchievement остаются как историческая запись.</summary>
    /// <response code="204">Удалено.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Тир не найден.</response>
    [HttpDelete("achievement-types/{id:guid}", Name = nameof(DeleteAchievementType))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAchievementType(Guid id)
    {
        await _achievementService.DeleteTypeAsync(id);
        return NoContent();
    }
}
