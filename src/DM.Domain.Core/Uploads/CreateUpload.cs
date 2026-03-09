using System;
using System.IO;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// DTO model for file upload
/// </summary>
public class CreateUpload
{
    /// <summary>
    /// File stream getting strategy
    /// </summary>
    public Func<Stream> StreamAccessor { get; set; } = null!;

    /// <summary>
    /// Parent entity identifier
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Original file name
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// File content type
    /// </summary>
    public string ContentType { get; set; } = null!;
}
