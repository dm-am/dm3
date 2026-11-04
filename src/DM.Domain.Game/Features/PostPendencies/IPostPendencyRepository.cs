using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.PostPendencies;

public interface IPostPendencyRepository
{
    Task<PostPendency?> Get(Guid pendencyId);

    Task<PostPendency> Create(CreatePostPendencyEntity entity);

    Task Delete(Guid pendencyId);

    /// <summary>
    /// Stamps every unfulfilled pendency of an active game that is due a reminder
    /// and returns the ones stamped.
    /// </summary>
    /// <remarks>
    /// The stamp is committed before the identifiers come back: it alone stops the reminder repeating every pass.
    /// </remarks>
    /// <returns>Identifiers of the pendencies to remind about.</returns>
    Task<IReadOnlyCollection<Guid>> ClaimPendingReminders(
        DateTimeOffset createdBefore,
        DateTimeOffset lastReminderBefore,
        DateTimeOffset remindedUtc,
        CancellationToken cancellationToken = default);
}
