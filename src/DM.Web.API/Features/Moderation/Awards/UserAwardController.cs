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

namespace DM.Web.API.Features.Moderation.Awards;

/// <summary>
/// Выдача и отзыв наград пользователям.
/// </summary>
/// <remarks>
/// Грант создает `UserAward` со ссылкой на `AwardType` (тип) и опционально
/// `ContestSeries` (серия конкурса). Отзыв — soft-delete: запись помечается
/// IsRemoved=true с указанием модератора и времени, физически не удаляется
/// (audit). Все ручки требуют SeniorModerator.
/// </remarks>
[ApiController]
[Route("v1/moderation")]
[ApiExplorerSettings(GroupName = "Moderation")]
[Tags("User Awards")]
[RequireRole(UserRole.SeniorModerator)]
public class UserAwardController : ControllerBase
{
    private readonly IAwardService _awardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public UserAwardController(IAwardService awardService, IMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    /// <summary>Выдать награду пользователю.</summary>
    /// <param name="username">Имя пользователя-получателя.</param>
    /// <param name="request">awardTypeId + опциональный contestSeriesId.</param>
    /// <response code="201">Награда выдана.</response>
    /// <response code="400">Тип/серия неактивны или невалидные данные.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Пользователь, тип или серия не найдены.</response>
    [HttpPost("users/{username}/awards", Name = nameof(GrantUserAward))]
    [ProducesResponseType(typeof(Envelope<UserAward>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BadRequestError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GrantUserAward(string username, [FromBody] GrantUserAwardRequest request)
    {
        var granted = await _awardService.GrantAsync(username, request.AwardTypeId, request.ContestSeriesId, request.WorkUrl);
        var api = _mapper.Map<UserAward>(granted);
        return CreatedAtRoute(
            nameof(AwardController.GetUserAwards),
            new { username },
            new Envelope<UserAward>(api));
    }

    /// <summary>Отозвать ранее выданную награду (soft-delete).</summary>
    /// <param name="username">Имя пользователя (для согласованности URL).</param>
    /// <param name="awardId">Идентификатор записи о выдаче.</param>
    /// <response code="204">Отозвано.</response>
    /// <response code="401">Не аутентифицирован.</response>
    /// <response code="403">Недостаточно прав.</response>
    /// <response code="404">Запись не найдена.</response>
    [HttpDelete("users/{username}/awards/{awardId:guid}", Name = nameof(RevokeUserAward))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeUserAward(string username, Guid awardId)
    {
        _ = username; // route param для консистентности URL, валидация по awardId
        await _awardService.RevokeAsync(awardId);
        return NoContent();
    }
}
