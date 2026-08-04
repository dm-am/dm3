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
}
