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
/// Not a row of Comments, so a conversation with moderation stays out of every
/// site-wide read of comments by construction. Never edited or deleted: it is the
/// record of how the ticket was settled.
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

    [ForeignKey(nameof(TicketId))]
    public virtual Ticket Ticket { get; set; } = null!;

    /// <summary>
    /// The author (null for the guest)
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User? Author { get; set; }

    #endregion
}
