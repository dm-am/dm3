namespace DM.Domain.Core.Enums;

/// <summary>
/// Who sees a file of a post: "Кто видит:" on its plate
/// </summary>
public enum UploadVisibleTo
{
    /// <summary>
    /// Every reader of the post, the default
    /// </summary>
    All = 0,

    /// <summary>
    /// The author, the leads and the characters chosen, as a hidden block addressed to them
    /// </summary>
    Selected = 1
}
