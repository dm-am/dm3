using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Personal.Notifications;

/// <summary>
/// Notification management
/// </summary>
/// <remarks>
/// Provides REST endpoints for reading and managing notifications.
/// Real-time notifications are delivered over the SignalR hub; its path is
/// declared where the hub is mapped and in API_DESIGN.md, and is not repeated
/// here — this third copy named an endpoint that never existed.
///
/// ## Notification Types
/// - New messages in conversations
/// - New comments on subscribed topics
/// - Character approval/rejection
/// - Game invitations
/// - And more (see EventType enum)
///
/// ## Bot Integration
/// Connect Telegram or Discord bots to receive notifications outside the website.
/// Use POST /bots/{type} to generate a linking code, then send it to the bot.
/// </remarks>
[ApiController]
[Route("v1/users/me/notifications")]
[ApiExplorerSettings(GroupName = "Personal")]
[Tags("Notifications")]
[AuthenticationRequired]
public class NotificationController : ControllerBase
{
    private readonly INotificationApiService _notificationApiService;

    /// <inheritdoc />
    public NotificationController(INotificationApiService notificationApiService)
    {
        _notificationApiService = notificationApiService;
    }

    /// <summary>
    /// Get my notifications
    /// </summary>
    /// <remarks>
    /// Returns paginated list of notifications for the authenticated user.
    /// Results are ordered by creation date (newest first).
    /// </remarks>
    /// <param name="skip">Number of notifications to skip (default: 0)</param>
    /// <param name="take">Number of notifications to take (default: 20, max: 100)</param>
    /// <response code="200">List of notifications</response>
    /// <response code="401">Authentication required</response>
    [HttpGet(Name = nameof(GetNotifications))]
    [ProducesResponseType(typeof(ListEnvelope<Notification>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        take = Math.Clamp(take, 1, 100);
        return Ok(new ListEnvelope<Notification>(await _notificationApiService.GetNotifications(skip, take)));
    }

    /// <summary>
    /// Get unread notifications count
    /// </summary>
    /// <remarks>
    /// Returns the count of unread notifications for the authenticated user.
    /// Useful for displaying notification badges.
    /// </remarks>
    /// <response code="200">Unread count</response>
    /// <response code="401">Authentication required</response>
    [HttpGet("unread", Name = nameof(GetUnreadCount))]
    [ProducesResponseType(typeof(NotificationCount), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount()
    {
        return Ok(await _notificationApiService.GetUnreadCount());
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    /// <remarks>
    /// Marks a single notification as read.
    /// </remarks>
    /// <param name="id">Notification identifier</param>
    /// <response code="204">Notification marked as read</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">Notification not found</response>
    [HttpDelete("{id:guid}/unread", Name = nameof(MarkNotificationAsRead))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNotificationAsRead(Guid id)
    {
        await _notificationApiService.MarkAsRead(id);
        return NoContent();
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    /// <remarks>
    /// Marks all notifications for the authenticated user as read.
    /// </remarks>
    /// <response code="204">All notifications marked as read</response>
    /// <response code="401">Authentication required</response>
    [HttpDelete("unread", Name = nameof(MarkNotificationsAsRead))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkNotificationsAsRead()
    {
        await _notificationApiService.MarkAsRead();
        return NoContent();
    }

    /// <summary>
    /// Get notification settings
    /// </summary>
    /// <remarks>
    /// Returns notification delivery preferences (Discord, Telegram channels).
    /// </remarks>
    /// <response code="200">Notification settings retrieved</response>
    /// <response code="401">Not authenticated</response>
    [HttpGet("settings", Name = nameof(GetNotificationSettings))]
    [ProducesResponseType(typeof(NotificationSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotificationSettings()
    {
        return Ok(await _notificationApiService.GetNotificationSettings());
    }

    /// <summary>
    /// Update notification settings
    /// </summary>
    /// <remarks>
    /// Updates notification delivery settings for Discord and Telegram channels.
    /// </remarks>
    /// <param name="request">Settings to update</param>
    /// <response code="200">Settings updated successfully</response>
    /// <response code="401">Not authenticated</response>
    [HttpPatch("settings", Name = nameof(UpdateNotificationSettings))]
    [ProducesResponseType(typeof(NotificationSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateNotificationSettings([FromBody] UpdateNotificationSettingsRequest request)
    {
        return Ok(await _notificationApiService.UpdateNotificationSettings(request));
    }

    #region Bot Integration

    /// <summary>
    /// Generate a linking code for notification bot
    /// </summary>
    /// <remarks>
    /// Returns a 6-digit code valid for 10 minutes.
    /// User should send this code to the bot to link their account.
    ///
    /// Supported bot types:
    /// - `telegram` - Link Telegram bot for notifications
    /// - `discord` - Link Discord bot for notifications
    /// </remarks>
    /// <param name="type">Bot type: telegram or discord</param>
    /// <response code="200">Linking code generated</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="400">Invalid bot type</response>
    [HttpPost("bots/{type}", Name = nameof(GenerateBotLinkCode))]
    [ProducesResponseType(typeof(BotLinkResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateBotLinkCode(string type) =>
        Ok(await _notificationApiService.ConnectBot(type));

    /// <summary>
    /// Disconnect notification bot
    /// </summary>
    /// <remarks>
    /// Removes the bot connection. User will no longer receive notifications via this channel.
    /// Also removes this channel from all category notification settings.
    /// </remarks>
    /// <param name="type">Bot type: telegram or discord</param>
    /// <response code="204">Bot disconnected</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="400">Invalid bot type</response>
    [HttpDelete("bots/{type}", Name = nameof(DisconnectBot))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DisconnectBot(string type)
    {
        await _notificationApiService.DisconnectBot(type);
        return NoContent();
    }

    #endregion
}
