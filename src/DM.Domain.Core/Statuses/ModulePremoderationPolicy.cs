using System;
using System.Net;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Statuses;

/// <summary>
/// What a premoderation move writes.
/// </summary>
/// <param name="Status">Premoderation status the module moves to.</param>
/// <param name="MentorId">Curator to record, or null to clear the current one.</param>
public sealed record ModulePremoderationChange(PremoderationStatus Status, Guid? MentorId);

/// <summary>
/// The premoderation state machine of a module: which move is legal from which
/// premoderation status, what status it produces, and who curates afterwards.
/// </summary>
/// <remarks>
/// One machine for a game and a blog, for the same reason as the status one
/// beside it. What each module keeps is its own admission — who may reach this
/// at all and how the module is fetched — which is where the two genuinely
/// differ; the rules themselves were a copy.
///
/// The curator is decided by both moves and by nothing else, so the caller
/// always writes the field: one move records the mentor who took the module in,
/// the other clears it.
/// </remarks>
public static class ModulePremoderationPolicy
{
    /// <summary>
    /// Apply a premoderation move to the status a module is in now.
    /// </summary>
    /// <param name="transition">Requested move.</param>
    /// <param name="current">Premoderation status the module is in now.</param>
    /// <param name="actingMentorId">The mentor performing the move.</param>
    /// <returns>The fields the move writes.</returns>
    /// <exception cref="HttpException">
    /// BadRequest, when the move is not legal from this status, or is not one of
    /// the moves the machine names.
    /// </exception>
    public static ModulePremoderationChange Resolve(
        ModulePremoderationTransition transition,
        PremoderationStatus current,
        Guid actingMentorId) =>
        transition switch
        {
            ModulePremoderationTransition.SendToPremoderation =>
                current == PremoderationStatus.AwaitingEdits
                    ? new ModulePremoderationChange(
                        PremoderationStatus.AwaitingApproval, actingMentorId)
                    : throw new HttpException(HttpStatusCode.BadRequest,
                        RefusalMessage.CannotSubmitForPremoderation(current)),

            ModulePremoderationTransition.RemoveFromPremoderation =>
                current == PremoderationStatus.AwaitingApproval
                    ? new ModulePremoderationChange(PremoderationStatus.Approved, null)
                    : throw new HttpException(HttpStatusCode.BadRequest,
                        RefusalMessage.CannotWithdrawFromPremoderation(current)),

            _ => throw new HttpException(HttpStatusCode.BadRequest,
                RefusalMessage.UnknownPremoderationTransition)
        };
}
