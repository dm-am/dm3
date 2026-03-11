using System;
using System.Threading.Tasks;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Shared interface for cleaning up obsolete uploads
/// </summary>
public interface IObsoleteUploadsCleanup
{
    /// <summary>
    /// Prepare obsolete images for deleting
    /// </summary>
    /// <param name="entityId">Entity identifier (user, character, etc.)</param>
    Task PrepareObsoleteForDeletingAsync(Guid entityId);
}
