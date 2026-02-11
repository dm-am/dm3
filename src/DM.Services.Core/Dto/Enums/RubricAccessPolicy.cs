namespace DM.Services.Core.Dto.Enums;

/// <summary>
/// Access level for a participant to a blog rubric
/// </summary>
public enum RubricAccessPolicy
{
    /// <summary>
    /// No access to the rubric
    /// </summary>
    NoAccess = 0,

    /// <summary>
    /// Read-only access (can view publications)
    /// </summary>
    ReadOnly = 1,

    /// <summary>
    /// Full access (can create and edit publications)
    /// </summary>
    Full = 2
}
