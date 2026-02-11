using System.Threading.Tasks;
using DM.Services.Community.BusinessProcesses.Account.BotLink;
using DM.Web.API.Dto.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Bot;

/// <summary>
/// Internal API for bot account linking verification
/// </summary>
[ApiController]
[Route("v1/bot")]
[ApiExplorerSettings(IgnoreApi = true)]
public class BotVerifyLinkController : ControllerBase
{
    private readonly IBotLinkService _botLinkService;

    /// <inheritdoc />
    public BotVerifyLinkController(IBotLinkService botLinkService)
    {
        _botLinkService = botLinkService;
    }

    /// <summary>
    /// Verify a bot linking code (called by bot)
    /// </summary>
    /// <param name="request">Bot link verification request</param>
    /// <response code="200">Link verified successfully</response>
    /// <response code="400">Invalid code or verification failed</response>
    [HttpPost("verify-link", Name = nameof(VerifyLink))]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> VerifyLink([FromBody] VerifyBotLinkRequest request)
    {
        var result = await _botLinkService.VerifyAndLink(
            request.Code, request.ChannelType, request.ExternalId);

        if (!result.Success)
            return BadRequest(new { error = result.Error });

        return Ok(new { userLogin = result.UserLogin });
    }
}
