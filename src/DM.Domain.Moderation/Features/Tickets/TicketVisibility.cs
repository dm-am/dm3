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
/// Two halves. The category: a junior moderator reads complaints about users and
/// suggestions, a senior moderator complaints about a junior moderator's decision
/// on top of that, an administrator everything; the support desk categories are
/// the administrators' alone. And the side: the moderator a complaint names as the
/// violator does not read it, does not answer it and does not close it, and is not
/// told it was filed - he is an ordinary user to it, and an ordinary user never
/// learns of a complaint about himself.
///
/// Written once because the paths to a ticket are many (the page, the answer, the
/// closing, the queue with its counters, the notifications of a filing and of a
/// reply), and a rule copied into each of them is forgotten in the next one. The
/// author of a ticket is not this rule's business: he reads his own ticket whatever
/// his role, and the service decides what he may do in it.
/// </remarks>
public static class TicketVisibility
{
    /// <summary>
    /// Categories a junior moderator reads.
    /// </summary>
    private static readonly TicketSubtype[] ModeratorSubtypes =
    [
        TicketSubtype.UserComplaint,
        TicketSubtype.SiteImprovementSuggestion
    ];

    /// <summary>
    /// Categories a senior moderator reads: a junior moderator's, and the
    /// complaints about a junior moderator's decisions.
    /// </summary>
    private static readonly TicketSubtype[] SeniorModeratorSubtypes =
    [
        TicketSubtype.UserComplaint,
        TicketSubtype.SiteImprovementSuggestion,
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
