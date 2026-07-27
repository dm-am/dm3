namespace DM.Domain.Core.Authorization;

/// <summary>
/// List of upload actions that require authorization
/// </summary>
public enum UploadIntention
{
    /// <summary>
    /// View upload (owner or moderator+)
    /// </summary>
    View = 1,

    /// <summary>
    /// Delete upload (owner or moderator+)
    /// </summary>
    Delete = 2,

    /// <summary>
    /// List all uploads across users (moderator+, per doc 4.2.3.8.9)
    /// </summary>
    ListAll = 3,

    /// <summary>
    /// List specific user's uploads (moderator+)
    /// </summary>
    ListUser = 4
}
