using System;

namespace DM.Domain.Core.Configuration;

/// <summary>
/// Product rules about the life of an uploaded file.
/// </summary>
public static class UploadPolicy
{
    /// <summary>
    /// How long a deleted file stays recoverable before its object is destroyed.
    /// </summary>
    /// <remarks>
    /// This is what makes "clicked delete, changed my mind" work, so it is a
    /// promise to the user rather than a sweeper setting: while it runs the row
    /// says the file is restorable and the object has to still be there.
    /// </remarks>
    public static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromHours(24);

    /// <summary>
    /// How many files one post may carry.
    /// </summary>
    /// <remarks>
    /// A post is a piece of writing with an illustration or two, not a folder. The
    /// number is checked when a file is attached rather than when the post is
    /// written, because the two arrive as separate requests and the post exists
    /// first.
    /// </remarks>
    public const int MaxPostAttachments = 3;

    /// <summary>
    /// Largest file that may be attached to a post.
    /// </summary>
    /// <remarks>
    /// Lower than the request-size ceiling the upload endpoint carries for every
    /// type: that one is a DoS bound, this one is the product rule for this type.
    /// A map is stored at full size — nothing downscales an attachment — so the
    /// byte count is the only thing keeping a room's worth of them bounded.
    /// </remarks>
    public const long MaxPostAttachmentSizeBytes = 5 * 1024 * 1024;
}
