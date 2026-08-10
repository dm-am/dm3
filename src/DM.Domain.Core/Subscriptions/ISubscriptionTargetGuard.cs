using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;

namespace DM.Domain.Core.Subscriptions;

/// <summary>
/// The rule a target puts on being subscribed to
/// </summary>
/// <remarks>
/// Subscribing is written from two places: the generic endpoint, which takes a
/// target type and an identifier and belongs to no module, and the module's own
/// handle, which knows what its target is. The rule lived only in the second
/// one, so the first created the same row without it — a game's blacklist
/// refuses a join, and a request naming that game by type and identifier walked
/// past the refusal and put the reader on the roster anyway.
///
/// The rule cannot move into the generic service: it belongs to the module that
/// owns the target, and the kernel is where the two meet. So the module declares
/// it here and the generic path asks.
///
/// The answer is a message rather than an exception because one violation has
/// more than one correct response: a subscriber refusing for themselves should
/// be told, and a roster built for somebody else should simply leave them off
/// rather than fail the action of the person building it.
/// </remarks>
public interface ISubscriptionTargetGuard
{
    /// <summary>
    /// Target type this guard speaks for
    /// </summary>
    SubscriptionTargetType TargetType { get; }

    /// <summary>
    /// Why this subscription is refused, or null when it is allowed
    /// </summary>
    /// <param name="targetId">Target of the subscription</param>
    /// <param name="subscriberId">Whoever would end up on the roster</param>
    /// <param name="ct">Cancellation token</param>
    Task<string?> Refusal(Guid targetId, Guid subscriberId, CancellationToken ct = default);
}
