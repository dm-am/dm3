using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Awards;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IAwardService = DM.Domain.Community.Features.Awards.IAwardService;
using DomainCreateAwardType = DM.Domain.Community.Features.Awards.CreateAwardType;
using DomainUpdateAwardType = DM.Domain.Community.Features.Awards.UpdateAwardType;
using DomainCreateContestSeries = DM.Domain.Community.Features.Awards.CreateContestSeries;
using DomainUpdateContestSeries = DM.Domain.Community.Features.Awards.UpdateContestSeries;

namespace DM.Web.API.Features.Moderation.Awards;

/// <summary>
/// CRUD каталога наград и серий конкурсов.
/// </summary>
/// <remarks>
/// Типы наград (`AwardType`) — timeless каталог (6 строк по умолчанию):
/// 1/2/3 место, народное признание, лучший критик, угадайка. Каждый тип
/// иммутабельный по семантике: переименовать можно, удалить — нельзя
/// (только деактивировать через PATCH IsActive=false), потому что
/// исторические UserAward ссылаются на тип через FK.
///
/// Серии конкурсов (`ContestSeries`) — каждый новый конкурс это новая
/// запись (сквозной Number в рамках типа, Year, TopicUrl).
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("Award Catalog")]
[RequireRole(UserRole.SeniorModerator)]
public class AwardCatalogController : ControllerBase
{
    private readonly IAwardService _awardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AwardCatalogController(IAwardService awardService, IMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    // ---- AwardType ----

    /// <summary>Создать новый тип награды в каталоге.</summary>
    /// <response code="201">Создан.</response>
    /// <response code="400">Неизвестная иконка или невалидные данные.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="409">Code уже занят.</response>
    [HttpPost("award-types", Name = nameof(CreateAwardType))]
    [ProducesResponseType(typeof(Envelope<AwardType>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAwardType([FromBody] CreateAwardTypeRequest request)
    {
        var domain = _mapper.Map<DomainCreateAwardType>(request);
        var created = await _awardService.CreateTypeAsync(domain);
        var api = _mapper.Map<AwardType>(created);
        return CreatedAtRoute(
            nameof(AwardController.GetAwardTypes),
            null,
            new Envelope<AwardType>(api));
    }

    /// <summary>Частичное обновление типа награды.</summary>
    /// <response code="200">Обновлен.</response>
    /// <response code="400">Неизвестная иконка или невалидные данные.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Тип не найден.</response>
    [HttpPatch("award-types/{id:guid}", Name = nameof(UpdateAwardType))]
    [ProducesResponseType(typeof(Envelope<AwardType>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAwardType(Guid id, [FromBody] UpdateAwardTypeRequest request)
    {
        var domain = _mapper.Map<DomainUpdateAwardType>(request);
        domain.Id = id;
        var updated = await _awardService.UpdateTypeAsync(domain);
        return Ok(new Envelope<AwardType>(_mapper.Map<AwardType>(updated)));
    }

    /// <summary>Деактивировать тип награды (IsActive=false). Уже выданные награды сохраняются.</summary>
    /// <response code="204">Деактивирован.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Тип не найден.</response>
    [HttpDelete("award-types/{id:guid}", Name = nameof(DeactivateAwardType))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAwardType(Guid id)
    {
        await _awardService.DeactivateTypeAsync(id);
        return NoContent();
    }

    // ---- ContestSeries ----

    /// <summary>Создать новую серию конкурса.</summary>
    /// <response code="201">Создана.</response>
    /// <response code="400">Невалидные данные.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="409">Серия с такой (ContestType, Number) уже существует.</response>
    [HttpPost("contest-series", Name = nameof(CreateContestSeries))]
    [ProducesResponseType(typeof(Envelope<ContestSeries>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateContestSeries([FromBody] CreateContestSeriesRequest request)
    {
        var domain = _mapper.Map<DomainCreateContestSeries>(request);
        var created = await _awardService.CreateSeriesAsync(domain);
        var api = _mapper.Map<ContestSeries>(created);
        return CreatedAtRoute(
            nameof(AwardController.GetContestSeries),
            null,
            new Envelope<ContestSeries>(api));
    }

    /// <summary>Частичное обновление серии конкурса.</summary>
    /// <response code="200">Обновлена.</response>
    /// <response code="400">Невалидные данные.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Серия не найдена.</response>
    [HttpPatch("contest-series/{id:guid}", Name = nameof(UpdateContestSeries))]
    [ProducesResponseType(typeof(Envelope<ContestSeries>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContestSeries(Guid id, [FromBody] UpdateContestSeriesRequest request)
    {
        var domain = _mapper.Map<DomainUpdateContestSeries>(request);
        domain.Id = id;
        var updated = await _awardService.UpdateSeriesAsync(domain);
        return Ok(new Envelope<ContestSeries>(_mapper.Map<ContestSeries>(updated)));
    }

    /// <summary>Деактивировать серию (IsActive=false). Уже выданные награды сохраняются.</summary>
    /// <response code="204">Деактивирована.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Серия не найдена.</response>
    [HttpDelete("contest-series/{id:guid}", Name = nameof(DeactivateContestSeries))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateContestSeries(Guid id)
    {
        await _awardService.DeactivateSeriesAsync(id);
        return NoContent();
    }
}
