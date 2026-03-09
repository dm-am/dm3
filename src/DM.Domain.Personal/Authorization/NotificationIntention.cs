namespace DM.Domain.Personal.Authorization;

/// <summary>
/// Intentions for notification management operations
/// </summary>
public enum NotificationIntention
{
    /// <summary>
    /// Generate bot link code for connecting notification channels (authenticated user)
    /// </summary>
    GenerateBotLinkCode,

    /// <summary>
    /// Disconnect bot channel from account (authenticated user)
    /// </summary>
    DisconnectBotChannel
}
