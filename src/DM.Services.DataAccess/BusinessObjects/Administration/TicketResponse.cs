using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Administration;

/// <summary>
/// DAL model of a response in a ticket conversation
/// </summary>
[Table("TicketResponses")]
public class TicketResponse
{
    /// <summary>
    /// Response identifier
    /// </summary>
    [Key]
    public Guid TicketResponseId { get; set; }

    /// <summary>
    /// Parent ticket identifier
    /// </summary>
    public Guid TicketId { get; set; }

    /// <summary>
    /// Author of the response
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Response text
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// When the response was created
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Whether this response is from a moderator (vs the user)
    /// </summary>
    public bool IsFromModerator { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Parent ticket
    /// </summary>
    [ForeignKey(nameof(TicketId))]
    public virtual Ticket Ticket { get; set; } = null!;

    /// <summary>
    /// Response author
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    #endregion
}
