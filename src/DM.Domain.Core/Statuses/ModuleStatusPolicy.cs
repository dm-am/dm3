using System.Net;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Statuses;

/// <summary>
/// The lifecycle fields a status move is decided from.
/// </summary>
/// <param name="Status">Status the module is in now.</param>
/// <param name="ClosedReason">Reason it is closed for; only stored while it is.</param>
/// <param name="ActivatedUtc">Moment it first became active, if it ever did.</param>
/// <param name="ClosedUtc">Moment it stopped, if it is stopped.</param>
public sealed record ModuleLifecycle(
    ModuleStatus Status,
    ClosedReason ClosedReason,
    DateTimeOffset? ActivatedUtc,
    DateTimeOffset? ClosedUtc);

/// <remarks>
/// A null field is one the move does not touch: the update entities of both
/// modules read null as "leave this column alone", which is how the first
/// activation and the moment of closing survive a move that passes over them a
/// second time.
/// </remarks>
/// <param name="ClosedReason">Reason to write, or null to leave the stored one.</param>
/// <param name="ActivatedUtc">Activation stamp to write, or null to keep the stored one.</param>
/// <param name="ClosedUtc">Closing stamp to write, or null to keep the stored one.</param>
/// <param name="ClearClosedUtc">Whether the closing stamp is dropped instead of written.</param>
public sealed record ModuleStatusChange(
    ModuleStatus Status,
    ClosedReason? ClosedReason,
    DateTimeOffset? ActivatedUtc,
    DateTimeOffset? ClosedUtc,
    bool ClearClosedUtc);

/// <summary>
/// The status state machine of a module: which move is legal from which state,
/// and what state it produces.
/// </summary>
/// <remarks>
/// One machine for a game and a blog; each module keeps its intention, its event
/// and, for a game, the recruitment flag. Pure: the caller hands it the moment.
/// An illegal move is BadRequest, authorization is the module's own later step.
/// </remarks>
public static class ModuleStatusPolicy
{
    /// <summary>
    /// Apply a move to the state a module is in now.
    /// </summary>
    /// <param name="transition">Requested move.</param>
    /// <param name="module">State the module is in now.</param>
    /// <param name="now">Moment the move happens at.</param>
    /// <returns>The fields the move writes.</returns>
    /// <exception cref="HttpException">
    /// BadRequest, when the move is not legal from this state, or is not one of
    /// the moves the machine names.
    /// </exception>
    public static ModuleStatusChange Resolve(
        ModuleStatusTransition transition, ModuleLifecycle module, DateTimeOffset now) =>
        transition switch
        {
            ModuleStatusTransition.Start => module.Status == ModuleStatus.Draft
                ? new ModuleStatusChange(
                    ModuleStatus.Active, null, FirstActivation(module, now), null, false)
                : throw Illegal(transition, module),

            ModuleStatusTransition.Freeze => module.Status == ModuleStatus.Active
                ? new ModuleStatusChange(
                    ModuleStatus.Closed, ClosedReason.Frozen, null, now, false)
                : throw Illegal(transition, module),

            ModuleStatusTransition.Finish => module.Status == ModuleStatus.Active
                ? new ModuleStatusChange(
                    ModuleStatus.Closed, ClosedReason.Finished, null, now, false)
                : throw Illegal(transition, module),

            // Active -> Closed+None, or Closed+Frozen -> Closed+None: a module
            // that was only paused can be given up on without being reopened.
            ModuleStatusTransition.Close => module.Status == ModuleStatus.Active ||
                (module.Status == ModuleStatus.Closed && module.ClosedReason == ClosedReason.Frozen)
                ? new ModuleStatusChange(
                    ModuleStatus.Closed, ClosedReason.None, null, FirstClosing(module, now), false)
                : throw Illegal(transition, module),

            ModuleStatusTransition.Reopen => module.Status == ModuleStatus.Closed
                ? new ModuleStatusChange(
                    ModuleStatus.Active, ClosedReason.None, FirstActivation(module, now), null, true)
                : throw Illegal(transition, module),

            _ => throw new HttpException(
                HttpStatusCode.BadRequest, RefusalMessage.UnknownStatusTransition)
        };

    // The moment of the first activation, which a later one does not overwrite.
    private static DateTimeOffset? FirstActivation(ModuleLifecycle module, DateTimeOffset now) =>
        module.ActivatedUtc.HasValue ? null : now;

    // The moment the module stopped, which closing an already frozen one keeps.
    private static DateTimeOffset? FirstClosing(ModuleLifecycle module, DateTimeOffset now) =>
        module.ClosedUtc.HasValue ? null : now;

    private static HttpException Illegal(
        ModuleStatusTransition transition, ModuleLifecycle module) =>
        new(HttpStatusCode.BadRequest, RefusalMessage.IllegalStatusTransition(
            transition, module.Status, module.ClosedReason));
}
