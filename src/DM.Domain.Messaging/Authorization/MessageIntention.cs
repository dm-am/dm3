namespace DM.Domain.Messaging.Authorization;

/// <summary>
/// List of message actions that requires authorization
/// </summary>
public enum MessageIntention
{
    /// <summary>
    /// Edit existing message
    /// </summary>
    Edit = 0,

    /// <summary>
    /// Delete existing message
    /// </summary>
    Delete = 1,

    /// <summary>
    /// Like or unlike message
    /// </summary>
    Like = 2
}
