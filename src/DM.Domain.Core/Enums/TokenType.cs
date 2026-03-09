namespace DM.Domain.Core.Enums;

/// <summary>
/// Authorization token type
/// </summary>
public enum TokenType
{
    // === ACCOUNT (verification) ===

    /// <summary>
    /// User registration password setting
    /// </summary>
    Activation = 0,

    /// <summary>
    /// Password restoration
    /// </summary>
    PasswordChange = 1,

    /// <summary>
    /// Email change confirmation
    /// </summary>
    EmailChange = 8,

    /// <summary>
    /// Notification bot account linking verification code
    /// </summary>
    NotificationBotLink = 7,

    // === GAME INVITATIONS ===

    /// <summary>
    /// Game assistant invitation
    /// </summary>
    GameAssistantInvitation = 2,

    /// <summary>
    /// Game player invitation
    /// </summary>
    GamePlayerInvitation = 3,

    /// <summary>
    /// Game reader invitation
    /// </summary>
    GameReaderInvitation = 4,

    // === BLOG INVITATIONS ===

    /// <summary>
    /// Blog assistant invitation
    /// </summary>
    BlogAssistantInvitation = 5,

    /// <summary>
    /// Blog reader invitation
    /// </summary>
    BlogReaderInvitation = 6
}
