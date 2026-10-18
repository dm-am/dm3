using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Moderation;

/// <summary>
/// DAL model of a comment in the conversation of a ticket: the author's reply or
/// moderation's answer
/// </summary>
/// <remarks>
/// A table of its own and not a row of Comments, the table of the discussions of
/// the site. That table feeds what the whole site reads - the count of comments
/// behind an achievement, the snapshot a warning takes of the text it names, the
/// open log that shows that snapshot to strangers - and a conversation with
/// moderation, about access recovery or personal data, belongs in none of them.
/// Kept apart here it is out of every one of those reads by construction, rather
/// than by a condition each of them would have to remember. The ticket repository
/// and the notifications about tickets are its only readers.
///
/// A comment is not edited and not deleted: the conversation is the record of how
/// the ticket was settled. It goes with its ticket (cascade), and the account of
/// its author cannot go from under it (restrict): an account is removed softly,
/// and a comment of a removed account stays, signed by nobody.
/// </remarks>
[Table("TicketComments")]
public class TicketComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    [Key]
    public Guid TicketCommentId { get; set; }

    /// <summary>
    /// The ticket the comment is written in
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// The author. Null only for the guest who filed the ticket, writing by its
    /// tracking token: no other path writes a comment without an account
    /// </summary>
    public Guid? AuthorId { get; set; }

    /// <summary>
    /// Text in the site markup, with the tag names the site writes
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The ticket
    /// </summary>
    [ForeignKey(nameof(TicketId))]
    public virtual Ticket Ticket { get; set; } = null!;

    /// <summary>
    /// The author (null for the guest)
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User? Author { get; set; }

    #endregion
}
