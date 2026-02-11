using System;
using System.Threading.Tasks;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Notifications;
using DM.Web.API.Services.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Notifications;

/// <summary>
/// Notification management endpoints
/// </summary>
/// <remarks>
/// Provides REST endpoints for reading and managing notifications.
/// Real-time notifications are delivered via SignalR hub at /notifications.
///
/// ## Notification Types
/// - New messages in conversations
/// - New comments on subscribed topics
/// - Character approval/rejection
/// - Game invitations
/// - And more (see EventType enum)
/// </remarks>
[ApiController]
[Route("v1/notifications")]
[ApiExplorerSettings(GroupName = "Common")]
[Tags("Notifications")]
public class NotificationController : ControllerBase
{
    private readonly INotificationApiService _notificationApiService;

    /// <inheritdoc />
    public NotificationController(INotificationApiService notificationApiService)
    {
        _notificationApiService = notificationApiService;
    }

    /// <summary>
    /// Get notifications for current user
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
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Notification>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20)
    {
        take = Math.Clamp(take, 1, 100);
        return Ok(await _notificationApiService.GetNotifications(skip, take));
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
    [HttpGet("unread/count", Name = nameof(GetUnreadCount))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(NotificationCount), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
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
    [HttpPost("{id}/read", Name = nameof(MarkNotificationAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
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
    [HttpPost("read-all", Name = nameof(MarkAllNotificationsAsRead))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> MarkAllNotificationsAsRead()
    {
        await _notificationApiService.MarkAllAsRead();
        return NoContent();
    }
}
