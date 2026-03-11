using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DM.Domain.Core.Uploads;

/// <summary>
/// Storage for public image uploads
/// </summary>
public interface IPublicImageUploadRepository
{
    /// <summary>
    /// Save image uploads
    /// </summary>
    /// <param name="uploads">Upload entity DTOs</param>
    /// <returns>Created uploads</returns>
    Task<IEnumerable<Upload>> CreateAsync(IEnumerable<UploadEntity> uploads);

    /// <summary>
    /// Mark obsolete (all but recently added) uploads for deleting
    /// </summary>
    /// <param name="entityId">Entity identifier</param>
    /// <returns></returns>
    Task RemoveObsoleteUploadsAsync(Guid entityId);
}
