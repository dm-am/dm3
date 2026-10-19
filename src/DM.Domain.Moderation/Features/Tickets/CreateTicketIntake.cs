using DM.Domain.Core.Enums;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// DTO for creating a ticket from the public intake forms (/support and /complaint).
/// Unlike <see cref="CreateTicket"/>, guests are allowed to submit and the
/// target user is optional.
/// </summary>
public class CreateTicketIntake
{
    public TicketSubtype Subtype { get; set; }

    /// <summary>
    /// Optional short subject line. The intake forms do not ask for one; the
    /// screens that read a ticket back name it by its category instead.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Ticket text in the site markup, the tag set of profile texts: neither
    /// [mod] nor [private]
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Contact for the reply (stored as guest email for unauthenticated authors)
    /// </summary>
    public string? Contact { get; set; }

    /// <summary>
    /// Optional link to the reported violation (complaints)
    /// </summary>
    public string? ViolationUrl { get; set; }

    /// <summary>
    /// Optional username of the user the complaint is about. Written into the text
    /// of the ticket as a line of its own, the way the link is
    /// </summary>
    public string? TargetUsername { get; set; }
}
