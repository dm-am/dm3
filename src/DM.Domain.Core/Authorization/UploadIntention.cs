namespace DM.Domain.Core.Authorization;

/// <summary>
/// List of upload actions that require authorization
/// </summary>
public enum UploadIntention
{
    /// <summary>
    /// View upload (owner or admin)
    /// </summary>
    View = 1,

    /// <summary>
    /// Delete upload (owner or admin)
    /// </summary>
    Delete = 2,

    /// <summary>
    /// List all uploads (admin only)
    /// </summary>
    ListAll = 3,

    /// <summary>
    /// List specific user's uploads (admin only)
    /// </summary>
    ListUser = 4
}
