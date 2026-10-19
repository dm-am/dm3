using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Moderation.Features.Warnings;

public interface IWarningRepository
{
    /// <summary>
    /// Get warnings for a user, newest first
    /// </summary>
    Task<IEnumerable<Warning>> GetUserWarnings(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get one page of the warning log, newest first, with the total the page
    /// control needs
    /// </summary>
    /// <remarks>
    /// The one read behind both lists: the open log and the moderation list differ
    /// in who may call them and in what is put on the wire, not in what is read.
    /// </remarks>
    /// <param name="targetUserId">Warned user to narrow the log to, null for the whole website</param>
    /// <param name="paging">Bounds of the page</param>
    /// <param name="ct">Cancellation token</param>
    Task<(IReadOnlyList<Warning> Items, int Total)> GetLog(
        Guid? targetUserId, PagingQuery paging, CancellationToken ct = default);

    /// <summary>
    /// Read one warning by its identifier, null when there is no such warning
    /// </summary>
    /// <remarks>
    /// The write paths read through this one: changing the points is allowed to
    /// the author of the warning as well as to a senior moderator, so the rule
    /// cannot be decided without the row it is about.
    /// </remarks>
    Task<Warning?> Get(Guid warningId, CancellationToken ct = default);

    Task<Warning> Create(CreateWarningEntity warning, CancellationToken ct = default);

    /// <summary>
    /// Change the points of a warning and sign the change. Answers whether the
    /// warning was there to be changed.
    /// </summary>
    /// <remarks>
    /// The warning itself stays as it was written: only the points move, and the
    /// signature of whoever moved them is written beside the points rather than
    /// over the moderator who issued the warning.
    /// </remarks>
    Task<bool> ChangePoints(Guid warningId, int points, Guid changedByUserId,
        DateTimeOffset changedUtc, string text, CancellationToken ct = default);

    /// <summary>
    /// The user a warning was issued to, null when there is no such warning
    /// </summary>
    Task<Guid?> GetTargetUserId(Guid warningId, CancellationToken ct = default);

    /// <summary>
    /// Delete the avatar a warning on a profile pinned, in the name of the
    /// moderator who asked. Answers whether the warning was there.
    /// </summary>
    /// <remarks>
    /// The upload is marked deleted as a moderator deletes a file of somebody else, which
    /// the log and the orphan sweeper read as moderation's. A mark already moderation's
    /// stays as it stands, so a second request moves neither its author nor its grace period.
    /// </remarks>
    Task<bool> DeleteProfilePicture(Guid warningId, Guid deletedByUserId,
        DateTimeOffset deletedUtc, CancellationToken ct = default);

    /// <summary>
    /// Get total warning points for a user, counted by the points as they stand
    /// now
    /// </summary>
    Task<int> GetUserWarningPoints(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get per-user aggregates of active warning points (only users with points > 0)
    /// </summary>
    Task<IEnumerable<UserWarningSummary>> GetActiveWarningSummaries(CancellationToken ct = default);
}
