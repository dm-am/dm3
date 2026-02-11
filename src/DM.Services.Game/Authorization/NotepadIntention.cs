namespace DM.Services.Game.Authorization;

/// <summary>
/// Notepad actions that require authorization
/// </summary>
public enum NotepadIntention
{
    /// <summary>
    /// Read entries from notepad
    /// </summary>
    Read = 1,

    /// <summary>
    /// Create entry in notepad
    /// </summary>
    Create = 2,

    /// <summary>
    /// Edit entry in notepad
    /// </summary>
    Edit = 3,

    /// <summary>
    /// Delete entry from notepad
    /// </summary>
    Delete = 4
}
