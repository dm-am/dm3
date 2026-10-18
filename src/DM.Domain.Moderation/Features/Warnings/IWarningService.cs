using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// Service for warning management
/// </summary>
public interface IWarningService
{
    /// <summary>
    /// Get warnings for a user
    /// </summary>
    Task<IEnumerable<Warning>> GetUserWarnings(string username, CancellationToken ct = default);

    /// <summary>
    /// Get a page of the warning log, open to everyone
    /// </summary>
    /// <remarks>
    /// No role is asked for and no evidence is read: a row of this log carries the
    /// warned reader, the moderator, the points, the reason and the moment.
    /// </remarks>
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

    /// <summary>
    /// Create a warning
    /// </summary>
    Task<Warning> CreateWarning(CreateWarning createWarning, CancellationToken ct = default);

    /// <summary>
    /// Change the points of a warning
    /// </summary>
    /// <remarks>
    /// The warning itself stays: it is the record of a decision and is never
    /// taken back. Only its points move, and the change is signed by whoever
    /// made it and explained in his own words, because one moderator wrote why
    /// he warned and another may be taking the points off for the opposite
    /// reason.
    /// </remarks>
    /// <param name="warningId">Warning to change</param>
    /// <param name="points">New points, 0 to 6</param>
    /// <param name="text">Why the points are being changed</param>
    /// <param name="ct">Cancellation token</param>
    Task<Warning> ChangeWarningPoints(Guid warningId, int? points, string text,
        CancellationToken ct = default);

    /// <summary>
    /// Take the picture out of a warning on a profile, for good (moderators only)
    /// </summary>
    /// <remarks>
    /// The avatar a warning on a profile pinned is kept from its owner and shown in
    /// the open log, and a picture that must not be shown even there is the one
    /// thing moderation could not reach once its owner replaced it. Everything
    /// else in the warning stays. Asking again, or about a warning that pins no
    /// picture, succeeds and changes nothing: what was asked for already holds.
    /// A warning issued to the moderator himself is refused: a party does not
    /// rule on his own case, and another moderator takes the picture out.
    /// </remarks>
    /// <param name="warningId">Warning whose picture to delete</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteProfilePicture(Guid warningId, CancellationToken ct = default);

    /// <summary>
    /// Get total warning points for a user
    /// </summary>
    Task<int> GetUserWarningPoints(string username, CancellationToken ct = default);

    /// <summary>
    /// Get violators: users with active warning points or an active ban (for moderators)
    /// </summary>
    Task<IEnumerable<Violator>> GetViolators(ViolatorsFilter filter = ViolatorsFilter.All, CancellationToken ct = default);
}

/// <summary>
/// DTO for creating a warning
/// </summary>
public class CreateWarning
{
    /// <summary>
    /// Target username
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// Entity ID that caused the warning. Not read for a warning on a profile,
    /// whose entity is the warned user.
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Entity type: Comment, Message, Topic, or Profile for a warning on the
    /// profile of the warned user
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Warning points (0-6, 0 = verbal warning without points)
    /// </summary>
    public int Points { get; set; } = 1;

    /// <summary>
    /// Warning reason
    /// </summary>
    public string Reason { get; set; } = "";
}

/// <summary>
/// DTO for changing the points of a warning
/// </summary>
public class ChangeWarningPoints
{
    /// <summary>
    /// New points (0-6, 0 = the warning stops weighing on the violation scale).
    /// Absent is refused: see the remarks of the request body
    /// </summary>
    public int? Points { get; set; }

    /// <summary>
    /// Why the points are being changed
    /// </summary>
    public string Reason { get; set; } = "";
}
