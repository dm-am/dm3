using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Account.BotLink;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Account;

/// <summary>
/// API for managing bot notification channel connections
/// </summary>
[ApiController]
[Route("v1/account/bot-link")]
[ApiExplorerSettings(GroupName = "Account")]
public class BotLinkController : ControllerBase
{
    private readonly IBotLinkService _botLinkService;

    /// <inheritdoc />
    public BotLinkController(IBotLinkService botLinkService)
    {
        _botLinkService = botLinkService;
    }

    /// <summary>
    /// Generate a linking code for Telegram bot
    /// </summary>
    /// <response code="200">Linking code generated</response>
    /// <response code="401">Not authenticated</response>
    [HttpPost("telegram", Name = nameof(GenerateTelegramCode))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<BotLinkResultDto>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GenerateTelegramCode()
    {
        var result = await _botLinkService.GenerateLinkCode("telegram");
        return Ok(new Envelope<BotLinkResultDto>(new BotLinkResultDto
        {
            Code = result.Code,
            ExpiresAt = result.ExpiresAt
        }));
    }

    /// <summary>
    /// Generate a linking code for Discord bot
    /// </summary>
    /// <response code="200">Linking code generated</response>
    /// <response code="401">Not authenticated</response>
    [HttpPost("discord", Name = nameof(GenerateDiscordCode))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<BotLinkResultDto>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GenerateDiscordCode()
    {
        var result = await _botLinkService.GenerateLinkCode("discord");
        return Ok(new Envelope<BotLinkResultDto>(new BotLinkResultDto
        {
            Code = result.Code,
            ExpiresAt = result.ExpiresAt
        }));
    }

    /// <summary>
    /// Disconnect Telegram bot
    /// </summary>
    /// <response code="204">Telegram disconnected</response>
    /// <response code="401">Not authenticated</response>
    [HttpDelete("telegram", Name = nameof(DisconnectTelegram))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> DisconnectTelegram()
    {
        await _botLinkService.Disconnect("telegram");
        return NoContent();
    }

    /// <summary>
    /// Disconnect Discord bot
    /// </summary>
    /// <response code="204">Discord disconnected</response>
    /// <response code="401">Not authenticated</response>
    [HttpDelete("discord", Name = nameof(DisconnectDiscord))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> DisconnectDiscord()
    {
        await _botLinkService.Disconnect("discord");
        return NoContent();
    }
}
