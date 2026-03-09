using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// DTO model for user upload
/// </summary>
public class Upload
{
    /// <summary>
    /// Upload identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// File owner
    /// </summary>
    public GeneralUser Owner { get; set; } = null!;

    /// <summary>
    /// File name to display
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// Path to file in storage
    /// </summary>
    public string FilePath { get; set; } = null!;
}
