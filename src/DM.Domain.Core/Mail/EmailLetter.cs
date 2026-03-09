namespace DM.Domain.Core.Mail;

/// <summary>
/// DTO model for sending email
/// </summary>
public class EmailLetter
{
    /// <summary>
    /// Email address to send letter to
    /// </summary>
    public required string Address { get; set; }

    /// <summary>
    /// Letter subject
    /// </summary>
    public required string Subject { get; set; }

    /// <summary>
    /// Letter body (HTML)
    /// </summary>
    public required string Body { get; set; }

    /// <summary>
    /// Embedded resources (CID attachments) for inline images
    /// </summary>
    public IReadOnlyList<LinkedResource> LinkedResources { get; set; } = [];
}
