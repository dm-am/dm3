namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Upload processing status
/// </summary>
public enum UploadStatus
{
    /// <summary>
    /// Upload initiated, waiting for file
    /// </summary>
    Pending = 0,

    /// <summary>
    /// File uploaded and processed successfully
    /// </summary>
    Confirmed = 1,

    /// <summary>
    /// Upload failed or expired
    /// </summary>
    Failed = 2
}
