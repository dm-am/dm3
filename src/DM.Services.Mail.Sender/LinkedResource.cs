namespace DM.Services.Mail.Sender;

/// <summary>
/// Embedded resource for email (CID attachment)
/// </summary>
public class LinkedResource
{
    /// <summary>
    /// Content ID for referencing in HTML (e.g., "logo" for cid:logo)
    /// </summary>
    public required string ContentId { get; init; }

    /// <summary>
    /// MIME type (e.g., "image/png")
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// Binary content
    /// </summary>
    public required byte[] Content { get; init; }
}
