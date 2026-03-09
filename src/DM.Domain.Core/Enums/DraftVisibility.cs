namespace DM.Domain.Core.Enums;

/// <summary>
/// Visibility of draft content (games/blogs in Draft status)
/// </summary>
public enum DraftVisibility
{
    /// <summary>
    /// Only users with roles (owner, assistants, readers, etc.) can view
    /// </summary>
    Private = 0,

    /// <summary>
    /// Preview visible to everyone
    /// </summary>
    Public = 1
}
