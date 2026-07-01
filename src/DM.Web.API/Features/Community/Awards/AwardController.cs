using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Community.Features.Awards;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Awards;

/// <summary>
/// Публичное чтение каталога наград, серий конкурсов и наград пользователей.
/// </summary>
/// <remarks>
/// Редактирование каталога (типы + серии) — в <c>v1/moderation/award-types</c>
/// и <c>v1/moderation/contest-series</c>. Выдача/отзыв награды — в
/// <c>v1/moderation/users/{username}/awards</c>.
/// </remarks>
[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Awards")]
public class AwardController : ControllerBase
{
    private readonly IAwardService _awardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public AwardController(IAwardService awardService, IMapper mapper)
    {
        _awardService = awardService;
        _mapper = mapper;
    }

    /// <summary>Активные типы наград (каталог).</summary>
    /// <response code="200">Список записей каталога.</response>
    [HttpGet("award-types", Name = nameof(GetAwardTypes))]
    [ProducesResponseType(typeof(ListEnvelope<AwardType>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAwardTypes()
    {
        var types = await _awardService.GetTypesAsync();
        var items = types.Select(_mapper.Map<AwardType>);
        return Ok(new ListEnvelope<AwardType>(items, null));
    }

    /// <summary>Активные серии конкурсов (для UI выдачи и подсветки итогов).</summary>
    /// <response code="200">Список серий, новые сверху (год DESC → сезон).</response>
    [HttpGet("contest-series", Name = nameof(GetContestSeries))]
    [ProducesResponseType(typeof(ListEnvelope<ContestSeries>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContestSeries()
    {
        var series = await _awardService.GetSeriesAsync();
        var items = series.Select(_mapper.Map<ContestSeries>);
        return Ok(new ListEnvelope<ContestSeries>(items, null));
    }

    /// <summary>Награды пользователя.</summary>
    /// <param name="username">Имя пользователя.</param>
    /// <response code="200">Список наград (новые серии сверху, внутри — по SortOrder типа).</response>
    /// <response code="404">Пользователь не найден.</response>
    [HttpGet("users/{username}/awards", Name = nameof(GetUserAwards))]
    [ProducesResponseType(typeof(ListEnvelope<UserAward>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAwards(string username)
    {
        var list = await _awardService.GetUserAwardsAsync(username);
        var items = list.Select(_mapper.Map<UserAward>);
        return Ok(new ListEnvelope<UserAward>(items, null));
    }
}
