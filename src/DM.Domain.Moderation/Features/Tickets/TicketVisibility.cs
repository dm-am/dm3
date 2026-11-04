using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Which moderator a ticket is moderation's business of: the one rule the ticket
/// service, the queue and the notifications about tickets all read.
/// </summary>
/// <remarks>
/// The moderator a complaint names as the violator does not read it, answer it or close
/// it, and is not told it was filed: to it he is an ordinary user, who never learns of a
/// complaint about himself. The author of a ticket reads his own whatever his role.
/// </remarks>
public static class TicketVisibility
{
    /// <summary>
    /// Categories a junior moderator reads.
    /// </summary>
    private static readonly TicketSubtype[] ModeratorSubtypes =
    [
        TicketSubtype.UserComplaint
    ];

    /// <summary>
    /// Categories a senior moderator reads: a junior moderator's, and the
    /// complaints about a junior moderator's decisions.
    /// </summary>
    private static readonly TicketSubtype[] SeniorModeratorSubtypes =
    [
        ..ModeratorSubtypes,
        TicketSubtype.ModeratorDecisionComplaint
    ];

    /// <summary>
    /// The categories the role reads, null meaning every one of them. Decided on
    /// the server, so that a junior moderator cannot ask for the administrators'
    /// categories by editing the query string. A role below moderator is answered
    /// the junior moderator's set: whether it reads anything at all is
    /// <see cref="Moderates" />'s question.
    /// </summary>
    public static IReadOnlyCollection<TicketSubtype>? Subtypes(UserRole role) =>
        role switch
        {
            UserRole.Admin or UserRole.System => null,
            UserRole.SeniorModerator => SeniorModeratorSubtypes,
            _ => ModeratorSubtypes
        };

    /// <summary>
    /// Whether the user moderates a ticket of this category naming this violator:
    /// a moderator whose role reads the category and who is not the violator.
    /// </summary>
    public static bool Moderates(Guid userId, UserRole role, TicketSubtype subtype, Guid? targetUserId) =>
        role >= UserRole.Moderator &&
        (Subtypes(role) is not { } subtypes || subtypes.Contains(subtype)) &&
        targetUserId != userId;
}
