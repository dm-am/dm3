using System;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// DTO for upload entity (used for repository operations)
/// </summary>
public class UploadEntity
{
    /// <summary>
    /// Upload identifier
    /// </summary>
    public Guid UploadId { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// User who uploaded the file
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Parent entity identifier
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Original file name
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// Path to file in storage
    /// </summary>
    public string FilePath { get; set; } = null!;

    /// <summary>
    /// Whether this is the original (not resized) image
    /// </summary>
    public bool Original { get; set; }

    /// <summary>
    /// Removal flag
    /// </summary>
    public bool IsRemoved { get; set; }
}
