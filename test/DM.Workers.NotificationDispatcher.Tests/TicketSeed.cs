using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Moderation;
using Microsoft.EntityFrameworkCore;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// The staff of a small site, the tickets they are told about and the
/// conversations of two of them.
/// </summary>
/// <remarks>
/// Every row makes one recipient rule distinguishable from the rule a generator
/// could get away with instead. The moderators are of every rank, so telling all
/// of them about a support ticket shows; one of them is named by a complaint and
/// one filed a complaint himself, so telling the side of a ticket shows; the
/// conversations have a moderator who answered beside one who did not, a writer
/// who is no longer a moderator and a writer whose account is gone, so telling
/// everybody on duty or everybody who ever wrote shows.
///
/// Written into the shared database under identifiers of its own, once for the
/// whole collection: the tests only read it.
/// </remarks>
internal static class TicketSeed
{
    private static Guid Id(string tail) => Guid.Parse("00000000-0000-0000-0d62-0000000000" + tail);

    /// <summary>A junior moderator who answers in the conversation of the moderator's complaint.</summary>
    public static readonly Guid JuniorModeratorId = Id("01");

    /// <summary>A senior moderator who answered the reader's complaint.</summary>
    public static readonly Guid SeniorModeratorId = Id("02");

    /// <summary>An administrator, who reads every category.</summary>
    public static readonly Guid AdminId = Id("03");

    /// <summary>A moderator the reader's complaint names as the violator.</summary>
    public static readonly Guid NamedModeratorId = Id("04");

    /// <summary>A moderator who filed a complaint about a reader himself.</summary>
    public static readonly Guid FilingModeratorId = Id("05");

    /// <summary>A reader who files tickets and answers in them.</summary>
    public static readonly Guid ReaderId = Id("06");

    /// <summary>A moderator whose account is removed.</summary>
    public static readonly Guid RemovedModeratorId = Id("07");

    /// <summary>A former moderator, an ordinary user now, who answered once.</summary>
    public static readonly Guid DemotedId = Id("08");

    public const string SeniorModeratorUsername = "seed-senior-mod";
    public const string AdminUsername = "seed-admin";
    public const string ReaderUsername = "seed-reader";
    public const string FilingModeratorUsername = "seed-filing-mod";

    /// <summary>The reader asks to recover his access: the administrators' category.</summary>
    public static readonly Guid AccessRecoveryTicketId = Id("11");

    /// <summary>The reader complains about the named moderator.</summary>
    public static readonly Guid ComplaintAboutAModeratorId = Id("12");

    /// <summary>The filing moderator complains about the reader.</summary>
    public static readonly Guid ComplaintByAModeratorId = Id("13");

    /// <summary>A guest reports a bug: no account behind it.</summary>
    public static readonly Guid GuestTicketId = Id("14");

    /// <summary>The senior moderator asks the reader for a link.</summary>
    public static readonly Guid SeniorAnswerId = Id("21");

    /// <summary>The reader answers with one.</summary>
    public static readonly Guid ReaderReplyId = Id("22");

    /// <summary>The former moderator answered while he still was one.</summary>
    public static readonly Guid DemotedAnswerId = Id("23");

    /// <summary>
    /// Written by the moderator the complaint names. The service answers him 404
    /// on every path into it, so this is the row that stands for whatever might
    /// still put him among the writers: the rule is the generator's to keep too.
    /// </summary>
    public static readonly Guid NamedModeratorCommentId = Id("24");

    /// <summary>Written by the moderator whose account is removed since.</summary>
    public static readonly Guid RemovedModeratorAnswerId = Id("25");

    /// <summary>The junior moderator asks the filing moderator.</summary>
    public static readonly Guid JuniorAnswerId = Id("26");

    /// <summary>The filing moderator answers as the author of his complaint.</summary>
    public static readonly Guid FilingModeratorReplyId = Id("27");

    /// <summary>The administrator asks the guest.</summary>
    public static readonly Guid AdminAnswerToTheGuestId = Id("28");

    /// <summary>The guest answers by his tracking token.</summary>
    public static readonly Guid GuestReplyId = Id("29");

    private static readonly DateTimeOffset Filed = DateTimeOffset.UtcNow.AddDays(-3);

    public static async Task ApplyAsync(NotificationDatabaseFixture fixture)
    {
        await using var context = fixture.CreateContext();

        // Keyed on a row this seed owns, for the reason NotificationSeed gives.
        // The query filter hides removed accounts, and the first row written is not
        // one of them.
        if (await context.Users.AnyAsync(u => u.UserId == JuniorModeratorId))
        {
            return;
        }

        context.Users.AddRange(
            Account(JuniorModeratorId, "seed-junior-mod", UserRole.Moderator),
            Account(SeniorModeratorId, SeniorModeratorUsername, UserRole.SeniorModerator),
            Account(AdminId, AdminUsername, UserRole.Admin),
            Account(NamedModeratorId, "seed-named-mod", UserRole.Moderator),
            Account(FilingModeratorId, FilingModeratorUsername, UserRole.Moderator),
            Account(ReaderId, ReaderUsername, UserRole.RegularUser),
            Account(RemovedModeratorId, "seed-removed-mod", UserRole.Moderator, removed: true),
            Account(DemotedId, "seed-demoted", UserRole.RegularUser));
        await context.SaveChangesAsync();

        context.Tickets.AddRange(
            Ticket(AccessRecoveryTicketId, TicketSubtype.AccessRecovery, ReaderId, targetId: null),
            Ticket(ComplaintAboutAModeratorId, TicketSubtype.UserComplaint, ReaderId, NamedModeratorId),
            Ticket(ComplaintByAModeratorId, TicketSubtype.UserComplaint, FilingModeratorId, ReaderId),
            Ticket(GuestTicketId, TicketSubtype.Bug, authorId: null, targetId: null));
        await context.SaveChangesAsync();

        context.TicketComments.AddRange(
            Comment(SeniorAnswerId, ComplaintAboutAModeratorId, SeniorModeratorId, 1),
            Comment(ReaderReplyId, ComplaintAboutAModeratorId, ReaderId, 2),
            Comment(DemotedAnswerId, ComplaintAboutAModeratorId, DemotedId, 3),
            Comment(NamedModeratorCommentId, ComplaintAboutAModeratorId, NamedModeratorId, 4),
            Comment(RemovedModeratorAnswerId, ComplaintAboutAModeratorId, RemovedModeratorId, 5),
            Comment(JuniorAnswerId, ComplaintByAModeratorId, JuniorModeratorId, 1),
            Comment(FilingModeratorReplyId, ComplaintByAModeratorId, FilingModeratorId, 2),
            Comment(AdminAnswerToTheGuestId, GuestTicketId, AdminId, 1),
            Comment(GuestReplyId, GuestTicketId, authorId: null, 2));
        await context.SaveChangesAsync();
    }

    private static User Account(Guid id, string username, UserRole role, bool removed = false)
    {
        var user = NotificationSeed.User(id, username, Filed.AddDays(-30));
        user.Role = role;
        user.IsRemoved = removed;
        return user;
    }

    private static Ticket Ticket(Guid ticketId, TicketSubtype subtype, Guid? authorId, Guid? targetId) => new()
    {
        TicketId = ticketId,
        UserId = authorId,
        TargetId = targetId,
        GuestEmail = authorId == null ? "guest@example.com" : null,
        EntityType = "",
        Status = TicketStatus.WaitingForModeration,
        Subtype = subtype,
        CreatedUtc = Filed,
        Description = "Текст обращения",
        Comment = ""
    };

    private static TicketComment Comment(Guid commentId, Guid ticketId, Guid? authorId, int hour) => new()
    {
        TicketCommentId = commentId,
        TicketId = ticketId,
        AuthorId = authorId,
        Text = "Текст комментария",
        CreatedUtc = Filed.AddHours(hour)
    };

    /// <summary>Reads a field off the anonymous metadata object.</summary>
    public static object? Field(DM.Domain.Personal.Features.Notifications.CreateNotification notification,
        string name)
    {
        var property = notification.Metadata.GetType().GetProperty(name);
        if (property == null)
        {
            throw new InvalidOperationException($"the metadata carries no {name}");
        }

        return property.GetValue(notification.Metadata);
    }

    /// <summary>Everybody a notification is addressed to, in one array.</summary>
    public static Guid[] Recipients(DM.Domain.Personal.Features.Notifications.CreateNotification[] produced) =>
        produced.SelectMany(notification => notification.UsersInterested).ToArray();
}
