namespace DM.Services.Community.BusinessProcesses.Chat;

/// <summary>
/// List of chat actions that requires authorization
/// </summary>
public enum ChatIntention
{
    /// <summary>
    /// Create new chat message
    /// </summary>
    CreateMessage = 0,

    /// <summary>
    /// Edit existing chat message
    /// </summary>
    EditMessage = 1,

    /// <summary>
    /// Delete existing chat message
    /// </summary>
    DeleteMessage = 2,

    /// <summary>
    /// Like or unlike chat message
    /// </summary>
    LikeMessage = 3
}