using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Uploads;

namespace DM.Infrastructure.Core.Storage;

/// <inheritdoc />
internal class UploadFactory : IUploadFactory
{
    private readonly IGuidFactory guidFactory;

    /// <inheritdoc />
    public UploadFactory(
        IGuidFactory guidFactory)
    {
        this.guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public UploadEntity Create(CreateUpload createUpload, string filePath, Guid userId, bool original,
        DateTimeOffset createdAt) => new()
    {
        UploadId = guidFactory.Create(),
        CreatedUtc = createdAt,
        UserId = userId,
        EntityId = createUpload.EntityId,
        FileName = createUpload.FileName,
        FilePath = filePath,
        Original = original,
        IsRemoved = false
    };
}
