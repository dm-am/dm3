using System.Threading.Tasks;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Personal.Subscriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Features.Community.Users;

/// <summary>
/// User subscribers (subscriptions to users)
/// </summary>
/// <remarks>
/// Endpoints for subscribing/unsubscribing to users and viewing subscribers list.
/// When you subscribe to a user, you receive notifications about their new content.
/// </remarks>
[ApiController]
[Route("v1/users")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Users")]
public class UserSubscriberController : ControllerBase
{
    private readonly IUserSubscriberApiService _userSubscriberApiService;

    /// <inheritdoc />
    public UserSubscriberController(IUserSubscriberApiService userSubscriberApiService)
    {
        _userSubscriberApiService = userSubscriberApiService;
    }

    /// <summary>
    /// Get user's subscribers
    /// </summary>
    /// <remarks>
    /// Returns list of users who are subscribed to the specified user.
    /// </remarks>
    /// <param name="username">Username of the target user</param>
    /// <response code="200">List of subscribers</response>
    /// <response code="404">User not found</response>
    [HttpGet("{username}/subscribers", Name = nameof(GetUserSubscribers))]
    [ProducesResponseType(typeof(ListEnvelope<User>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserSubscribers(string username) =>
        Ok(await _userSubscriberApiService.GetSubscribersAsync(username));

    /// <summary>
    /// Subscribe to a user
    /// </summary>
    /// <remarks>
    /// Subscribes current user to receive notifications about the specified user's new content.
    /// </remarks>
    /// <param name="username">Username to subscribe to</param>
    /// <response code="201">Successfully subscribed to the user</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Cannot subscribe to yourself</response>
    /// <response code="404">User not found</response>
    [HttpPost("{username}/subscribers", Name = nameof(SubscribeToUser))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Subscription), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubscribeToUser(string username) =>
        CreatedAtRoute(nameof(GetMySubscriptionStatus), new { username }, await _userSubscriberApiService.SubscribeAsync(username));

    /// <summary>
    /// Unsubscribe from a user
    /// </summary>
    /// <remarks>
    /// Removes current user's subscription to the specified user.
    /// </remarks>
    /// <param name="username">Username to unsubscribe from</param>
    /// <response code="204">Successfully unsubscribed from the user</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">User not found</response>
    [HttpDelete("{username}/subscribers", Name = nameof(UnsubscribeFromUser))]
    [AuthenticationRequired]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnsubscribeFromUser(string username)
    {
        await _userSubscriberApiService.UnsubscribeAsync(username);
        return NoContent();
    }

    /// <summary>
    /// Check if current user is subscribed to a user
    /// </summary>
    /// <remarks>
    /// Returns subscription details if current user is subscribed to the specified user.
    /// </remarks>
    /// <param name="username">Username to check</param>
    /// <response code="200">Subscription status with details if subscribed</response>
    /// <response code="404">User not found</response>
    [HttpGet("{username}/subscribers/me", Name = nameof(GetMySubscriptionStatus))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Subscription), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySubscriptionStatus(string username)
    {
        var subscription = await _userSubscriberApiService.GetSubscriptionStatusAsync(username);
        return subscription == null ? NoContent() : Ok(subscription);
    }
}
