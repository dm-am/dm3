namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// BB text parse mode
/// </summary>
public enum BbParseMode
{
    /// <summary>
    /// General text parse mode (used for messages, comments, etc.)
    /// </summary>
    Common = 0,

    /// <summary>
    /// General information parse mode
    /// </summary>
    Info = 1,

    /// <summary>
    /// Game post parse mode
    /// </summary>
    Post = 3
}