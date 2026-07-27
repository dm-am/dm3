using System;
using System.Threading.Tasks;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Deletes obsolete uploads (old avatars after replacement, soft-deleted
/// records) — removes orphan objects from S3 and hard-deletes DB records.
///
/// Called from <c>UserService.UpdateAsync</c> on PATCH avatarUploadId
/// (synchronous cleanup of the user's previous avatars), and also from
/// a background worker every N hours to sweep uploads marked
/// IsRemoved=true manually or by other services.
/// </summary>
public interface IUploadGarbageCollector
{
    /// <summary>
    /// Process an entity's uploads after replacement: keep only the most
    /// recent one (by CreatedUtc), mark the rest IsRemoved and delete
    /// the related S3 objects (FilePath / MediumFilePath / SmallFilePath).
    /// </summary>
    /// <param name="entityId">Entity identifier (User/Character).</param>
    Task CollectObsoleteAsync(Guid entityId);
}
