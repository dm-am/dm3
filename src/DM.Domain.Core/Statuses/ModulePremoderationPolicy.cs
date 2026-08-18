using System;
using System.Net;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;

namespace DM.Domain.Core.Statuses;

/// <summary>
/// What a premoderation move writes.
/// </summary>
/// <param name="Status">Premoderation status the module moves to.</param>
/// <param name="MentorId">Curator to record, or null to clear the current one.</param>
/// <param name="SetMentorId">
/// Whether the move touches the curator at all. False leaves whoever is recorded
/// in place: the author's move is not a change of curator, and writing the author
/// into that field would make them the mentor of their own module.
/// </param>
public sealed record ModulePremoderationChange(
    PremoderationStatus Status, Guid? MentorId, bool SetMentorId);

/// <summary>
/// The premoderation state machine of a module: which status a module is born in,
/// which move is legal from which status, what status it produces, and who
/// curates afterwards.
/// </summary>
/// <remarks>
/// One machine for a game and a blog, for the same reason as the status one
/// beside it. What each module keeps is its own admission — who may reach this
/// at all and how the module is fetched — which is where the two genuinely
/// differ; the rules themselves were a copy.
///
/// Three moves and no more. Two of them are the moderation verdict and are legal
/// from every status: a mentor decides, and a decision that could only be made
/// from one particular state would be a machine deciding instead. The third is
/// the author's request, and it is the only one the machine can refuse.
/// </remarks>
public static class ModulePremoderationPolicy
{
    /// <summary>
    /// The premoderation status a module is created in.
    /// </summary>
    /// <remarks>
    /// The whole reason premoderation exists, and until this method was called
    /// from the creation paths it existed nowhere: both modules were created with
    /// the default zero, which is Approved, so nothing was ever premoderated. The
    /// rule is one rule and lives once — a game and a blog ask the same question
    /// of the same author.
    ///
    /// Two kinds of author are held back. A newbie, which the site computes from
    /// their post count, and a user moderation has put under watch by hand, which
    /// nothing computes and nothing expires: that flag is set and cleared by a
    /// moderator and outlives the warnings and bans that prompted it.
    /// </remarks>
    /// <param name="author">The user creating the game or the blog.</param>
    /// <returns>Premoderation status to store on the new module.</returns>
    public static PremoderationStatus InitialStatus(IUser author) =>
        author.IsNewbie || author.IsUnderModerationWatch
            ? PremoderationStatus.AwaitingEdits
            : PremoderationStatus.Approved;

    /// <summary>
    /// Apply a premoderation move to the status a module is in now.
    /// </summary>
    /// <param name="transition">Requested move.</param>
    /// <param name="current">Premoderation status the module is in now.</param>
    /// <param name="actingUserId">The user performing the move.</param>
    /// <returns>The fields the move writes.</returns>
    /// <exception cref="HttpException">
    /// BadRequest, when the move is not legal from this status, or is not one of
    /// the moves the machine names.
    /// </exception>
    public static ModulePremoderationChange Resolve(
        ModulePremoderationTransition transition,
        PremoderationStatus current,
        Guid actingUserId) =>
        transition switch
        {
            // Approving releases the module, so nobody has to answer for it any
            // more and the curator is cleared. Legal from Approved as well: the
            // move is idempotent on purpose, so two mentors reaching the same
            // verdict do not turn the second one into a 400.
            ModulePremoderationTransition.SetApproved =>
                new ModulePremoderationChange(PremoderationStatus.Approved, null, true),

            // Returning a module for edits is the moment somebody starts answering
            // for it, which is what the curator field records — so it names the
            // mentor who made the call, not whoever happened to hold it before.
            ModulePremoderationTransition.SetAwaitingEdits =>
                new ModulePremoderationChange(
                    PremoderationStatus.AwaitingEdits, actingUserId, true),

            // The one refusable move. An author whose module is already awaiting a
            // verdict, or already approved, has nothing to ask for.
            ModulePremoderationTransition.SubmitForApproval =>
                current == PremoderationStatus.AwaitingEdits
                    ? new ModulePremoderationChange(
                        PremoderationStatus.AwaitingApproval, null, false)
                    : throw new HttpException(HttpStatusCode.BadRequest,
                        RefusalMessage.CannotSubmitForApproval(current)),

            _ => throw new HttpException(HttpStatusCode.BadRequest,
                RefusalMessage.UnknownPremoderationTransition)
        };
}
