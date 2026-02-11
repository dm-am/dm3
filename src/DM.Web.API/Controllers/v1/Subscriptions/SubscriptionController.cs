using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Authentication;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Subscriptions;
using DM.Web.API.Services.Subscriptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DM.Web.API.Controllers.v1.Subscriptions;

/// <summary>
/// API controller for managing user subscriptions
/// </summary>
/// <remarks>
/// Subscriptions allow users to follow games, blogs, authors, boards, and other content.
/// Users receive notifications based on their subscription settings.
/// </remarks>
[ApiController]
[Route("v1/subscriptions")]
[ApiExplorerSettings(GroupName = "Common")]
[Tags("Subscriptions")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionApiService _apiService;

    /// <inheritdoc />
    public SubscriptionController(ISubscriptionApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Get all subscriptions for the current user
    /// </summary>
    /// <remarks>
    /// Returns all subscriptions for the authenticated user.
    /// </remarks>
    /// <response code="200">List of subscriptions</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet(Name = nameof(GetMySubscriptions))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Subscription>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetMySubscriptions() =>
        Ok(await _apiService.GetMySubscriptions());

    /// <summary>
    /// Get subscriptions by target type
    /// </summary>
    /// <remarks>
    /// Returns subscriptions filtered by target type (Game, Blog, Author, etc.)
    /// </remarks>
    /// <param name="targetType">Target type to filter by</param>
    /// <response code="200">Filtered list of subscriptions</response>
    /// <response code="401">User must be authenticated</response>
    [HttpGet("by-type/{targetType}", Name = nameof(GetMySubscriptionsByType))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(ListEnvelope<Subscription>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> GetMySubscriptionsByType(SubscriptionTargetType targetType) =>
        Ok(await _apiService.GetMySubscriptions(targetType));

    /// <summary>
    /// Get subscription by ID
    /// </summary>
    /// <param name="id">Subscription identifier</param>
    /// <response code="200">Subscription details</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Subscription not found</response>
    [HttpGet("{id}", Name = nameof(GetSubscription))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Subscription>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public Task<IActionResult> GetSubscription(Guid id)
    {
        // This endpoint requires GetById which is not implemented yet
        // Return not found for now
        return Task.FromResult<IActionResult>(NotFound());
    }

    /// <summary>
    /// Check subscription status for a target
    /// </summary>
    /// <remarks>
    /// Check if the current user is subscribed to a specific target.
    /// </remarks>
    /// <param name="targetType">Target type (Game, Blog, Author, etc.)</param>
    /// <param name="targetId">Target entity identifier</param>
    /// <response code="200">Subscription found</response>
    /// <response code="401">User must be authenticated</response>
    /// <response code="404">Not subscribed to this target</response>
    [HttpGet("check/{targetType}/{targetId}", Name = nameof(CheckSubscription))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Subscription>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> CheckSubscription(SubscriptionTargetType targetType, Guid targetId)
    {
        var result = await _apiService.GetSubscription(targetType, targetId);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    /// <summary>
    /// Subscribe to a target
    /// </summary>
    /// <remarks>
    /// Creates a subscription to the specified target. If already subscribed, returns the existing subscription.
    /// </remarks>
    /// <param name="targetType">Target type (Game, Blog, Author, etc.)</param>
    /// <param name="targetId">Target entity identifier</param>
    /// <param name="request">Optional subscription settings</param>
    /// <response code="201">Subscription created</response>
    /// <response code="200">Already subscribed, returns existing subscription</response>
    /// <response code="401">User must be authenticated</response>
    [HttpPost("{targetType}/{targetId}", Name = nameof(Subscribe))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Subscription>), 201)]
    [ProducesResponseType(typeof(Envelope<Subscription>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    public async Task<IActionResult> Subscribe(
        SubscriptionTargetType targetType,
        Guid targetId,
        [FromBody] SubscribeRequest? request = null)
    {
        var result = await _apiService.Subscribe(targetType, targetId, request);
        return CreatedAtRoute(nameof(CheckSubscription), new { targetType, targetId }, result);
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
    [HttpPatch("{id}", Name = nameof(UpdateSubscription))]
    [AuthenticationRequired]
    [ProducesResponseType(typeof(Envelope<Subscription>), 200)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    [ProducesResponseType(typeof(GeneralError), 404)]
    public async Task<IActionResult> UpdateSubscription(
        Guid id,
        [FromBody] UpdateSubscriptionRequest request) =>
        Ok(await _apiService.UpdateSettings(id, request));

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
    [HttpDelete("{id}", Name = nameof(Unsubscribe))]
    [AuthenticationRequired]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(GeneralError), 401)]
    [ProducesResponseType(typeof(GeneralError), 403)]
    public async Task<IActionResult> Unsubscribe(Guid id)
    {
        await _apiService.Unsubscribe(id);
        return NoContent();
    }
}
