using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using DM.Web.API.Shared.RateLimiting;

namespace DM.Web.API.Features.Personal.Subscriptions;

/// <summary>
/// Subscription management
/// </summary>
/// <remarks>
/// Subscriptions allow users to subscribe to games, blogs, authors, boards, and other content.
/// Users receive notifications based on their subscription settings.
/// </remarks>
[ApiController]
[Route("v1/users/me/subscriptions")]
[ApiExplorerSettings(GroupName = "Personal")]
[Tags("Subscriptions")]
[AuthenticationRequired]
[EnableRateLimiting(RateLimitPolicies.Default)]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionApiService _apiService;

    /// <inheritdoc />
    public SubscriptionController(ISubscriptionApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get all my subscriptions
    /// </summary>
    /// <remarks>
    /// Returns all subscriptions for the authenticated user.
    /// Use ?type= query parameter to filter by target type.
    /// </remarks>
    /// <param name="type">Optional: Filter by target type (Game, Blog, Author, etc.)</param>
    /// <response code="200">List of subscriptions</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet(Name = nameof(GetMySubscriptions))]
    [ProducesResponseType(typeof(ListEnvelope<Subscription>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySubscriptions([FromQuery] SubscriptionTargetType? type = null)
    {
        if (type.HasValue)
        {
            return Ok(new ListEnvelope<Subscription>(await _apiService.GetMySubscriptionsAsync(type.Value)));
        }
        return Ok(new ListEnvelope<Subscription>(await _apiService.GetMySubscriptionsAsync()));
    }

    /// <summary>
    /// Get subscription by ID
    /// </summary>
    /// <param name="id">Subscription identifier</param>
    /// <response code="200">Subscription details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Subscription not found</response>
    [HttpGet("{id:guid}", Name = nameof(GetSubscription))]
    [ProducesResponseType(typeof(Subscription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubscription(Guid id)
    {
        var result = await _apiService.GetByIdAsync(id);
        if (result == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Subscription not found");
        }
        return Ok(result);
    }

    /// <summary>
    /// Check subscription status
    /// </summary>
    /// <remarks>
    /// Check if the current user is subscribed to a specific target.
    /// </remarks>
    /// <param name="type">Target type (Game, Blog, Author, etc.)</param>
    /// <param name="targetId">Target entity identifier</param>
    /// <response code="200">Subscription found</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Not subscribed to this target</response>
    [HttpGet("check", Name = nameof(CheckSubscription))]
    [ProducesResponseType(typeof(Subscription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckSubscription(
        [FromQuery] SubscriptionTargetType type,
        [FromQuery] Guid targetId)
    {
        var result = await _apiService.GetSubscriptionAsync(type, targetId);
        if (result == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, "Not subscribed to this target");
        }
        return Ok(result);
    }

    /// <summary>
    /// Subscribe to a target
    /// </summary>
    /// <remarks>
    /// Creates a subscription to the specified target. If already subscribed, returns the existing subscription.
    /// </remarks>
    /// <param name="request">Subscription request</param>
    /// <response code="201">Subscription created</response>
    /// <response code="200">Already subscribed, returns existing subscription</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost(Name = nameof(Subscribe))]
    [ProducesResponseType(typeof(Subscription), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Subscription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        var result = await _apiService.SubscribeAsync(request.TargetType, request.TargetId, request);
        return CreatedAtRoute(nameof(CheckSubscription), new { type = request.TargetType, targetId = request.TargetId }, result);
    }

    /// <summary>
    /// Update subscription settings
    /// </summary>
    /// <remarks>
    /// Updates notification settings for an existing subscription.
    /// </remarks>
    /// <param name="id">Subscription identifier</param>
    /// <param name="request">New subscription settings</param>
    /// <response code="200">Subscription updated</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Cannot modify another user's subscription</response>
    /// <response code="404">Subscription not found</response>
    [HttpPatch("{id:guid}", Name = nameof(UpdateSubscription))]
    [ProducesResponseType(typeof(Subscription), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSubscription(
        Guid id,
        [FromBody] UpdateSubscriptionRequest request) =>
        Ok(await _apiService.UpdateSettingsAsync(id, request));

    /// <summary>
    /// Unsubscribe
    /// </summary>
    /// <remarks>
    /// Removes the subscription. If not subscribed, returns success.
    /// </remarks>
    /// <param name="id">Subscription identifier</param>
    /// <response code="204">Unsubscribed successfully</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="403">Cannot delete another user's subscription</response>
    /// <response code="404">Subscription not found</response>
    [HttpDelete("{id:guid}", Name = nameof(Unsubscribe))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(GeneralError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unsubscribe(Guid id)
    {
        await _apiService.UnsubscribeAsync(id);
        return NoContent();
    }
}
