using System;
using DM.Domain.Core.Uploads;

namespace DM.Infrastructure.Core.Storage;

/// <summary>
/// Factory for upload entity model
/// </summary>
internal interface IUploadFactory
{
    /// <summary>
    /// Create new upload entity
    /// </summary>
    /// <param name="createUpload">Upload creation data</param>
    /// <param name="filePath">Path to the stored file</param>
    /// <param name="userId">User identifier</param>
    /// <param name="original">Whether this is the original image</param>
    /// <param name="createdAt">Creation timestamp</param>
    /// <returns>Upload entity DTO</returns>
    UploadEntity Create(CreateUpload createUpload, string filePath, Guid userId, bool original, DateTimeOffset createdAt);
}
