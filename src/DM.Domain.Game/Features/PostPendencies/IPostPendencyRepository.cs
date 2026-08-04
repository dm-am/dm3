using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.PostPendencies;

/// <summary>
/// Repository for post pendency operations
/// </summary>
public interface IPostPendencyRepository
{
    /// <summary>
    /// Get post pendency by ID
    /// </summary>
    Task<PostPendency?> Get(Guid pendencyId);

    /// <summary>
    /// Create post pendency
    /// </summary>
    Task<PostPendency> Create(CreatePostPendencyEntity entity);

    /// <summary>
    /// Delete post pendency
    /// </summary>
    Task Delete(Guid pendencyId);

    /// <summary>
    /// Stamps every unfulfilled pendency of an active game that is due a reminder
    /// and returns the ones stamped.
    /// </summary>
    /// <remarks>
    /// Selecting and stamping are one call, and the stamp is committed before the
    /// identifiers are handed back. The marker is the only thing that stops the
    /// same reminder going out on every pass: a caller that published first and
    /// saved afterwards would send letters and bot messages the database holds no
    /// record of, and send them again on the next pass, and the one after that,
    /// until one save finally landed.
    /// </remarks>
    /// <param name="createdBefore">Cutoff for the first reminder.</param>
    /// <param name="lastReminderBefore">Cutoff for a repeat reminder.</param>
    /// <param name="remindedUtc">Moment written on the pendencies claimed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Identifiers of the pendencies to remind about.</returns>
    Task<IReadOnlyCollection<Guid>> ClaimPendingReminders(
        DateTimeOffset createdBefore,
        DateTimeOffset lastReminderBefore,
        DateTimeOffset remindedUtc,
        CancellationToken cancellationToken = default);
}
