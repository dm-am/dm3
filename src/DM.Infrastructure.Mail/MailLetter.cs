using System.Collections.Generic;

namespace DM.Infrastructure.Mail;

/// <summary>
/// DTO model for sending email.
/// </summary>
/// <remarks>
/// DEPRECATED: Use <see cref="DM.Domain.Core.Mail.EmailLetter"/> from Domain.Core.Mail instead.
/// This class will be removed after migration is complete.
/// </remarks>
public class MailLetter
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
    /// Letter body
    /// </summary>
    public required string Body { get; set; }

    /// <summary>
    /// Embedded resources (CID attachments) for inline images
    /// </summary>
    public IReadOnlyList<LinkedResource> LinkedResources { get; set; } = [];
}
