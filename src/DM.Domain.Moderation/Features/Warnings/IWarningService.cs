using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Moderation.Features.Warnings;

public interface IWarningService
{
    Task<IEnumerable<Warning>> GetUserWarnings(string username, CancellationToken ct = default);

    /// <summary>
    /// Get a page of the warning log, open to everyone: no role is asked for and no
    /// evidence is read
    /// </summary>
    /// <param name="username">Username the log is narrowed to, empty for the whole website</param>
    /// <param name="paging">Bounds of the page</param>
    /// <param name="ct">Cancellation token</param>
    Task<(IReadOnlyList<Warning> Warnings, PagingResult Paging)> GetLog(
        string? username, PagingQuery paging, CancellationToken ct = default);

    /// <summary>
    /// Get a page of the warning log with the evidence (for moderators)
    /// </summary>
    /// <param name="username">Username the log is narrowed to, empty for the whole website</param>
    /// <param name="paging">Bounds of the page</param>
    /// <param name="ct">Cancellation token</param>
    Task<(IReadOnlyList<Warning> Warnings, PagingResult Paging)> GetAllWarnings(
        string? username, PagingQuery paging, CancellationToken ct = default);

    Task<Warning> CreateWarning(CreateWarning createWarning, CancellationToken ct = default);

    /// <summary>
    /// Change the points of a warning, explained in the words of whoever changes them.
    /// The warning itself, the record of a decision, stays as it was written.
    /// </summary>
    Task<Warning> ChangeWarningPoints(Guid warningId, ChangeWarningPoints change,
        CancellationToken ct = default);

    /// <summary>
    /// Take the picture out of a warning on a profile, for good (moderators only).
    /// Everything else in the warning stays, and asking again succeeds and changes
    /// nothing. A warning issued to the moderator himself is refused: another moderator
    /// takes the picture out.
    /// </summary>
    Task DeleteProfilePicture(Guid warningId, CancellationToken ct = default);

    Task<int> GetUserWarningPoints(string username, CancellationToken ct = default);

    /// <summary>
    /// Get violators: users with active warning points or an active ban (for moderators)
    /// </summary>
    Task<IEnumerable<Violator>> GetViolators(ViolatorsFilter filter = ViolatorsFilter.All, CancellationToken ct = default);
}
