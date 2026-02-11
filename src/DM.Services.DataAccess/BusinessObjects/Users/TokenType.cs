namespace DM.Services.DataAccess.BusinessObjects.Users;

/// <summary>
/// Authorised action type
/// </summary>
public enum TokenType
{
    /// <summary>
    /// User registration password setting
    /// </summary>
    Activation = 0,

    /// <summary>
    /// Password restoration
    /// </summary>
    PasswordChange = 1,

    /// <summary>
    /// Game assistant assignment
    /// </summary>
    AssistantAssignment = 2,

    /// <summary>
    /// Game player invitation
    /// </summary>
    PlayerInvitation = 3,

    /// <summary>
    /// Game reader invitation
    /// </summary>
    ReaderInvitation = 4,

    /// <summary>
    /// Blog assistant invitation
    /// </summary>
    BlogAssistantInvitation = 5,

    /// <summary>
    /// Blog reader invitation
    /// </summary>
    BlogReaderInvitation = 6,

    /// <summary>
    /// Bot account linking verification code
    /// </summary>
    BotLink = 7,

    /// <summary>
    /// Email change confirmation
    /// </summary>
    EmailChange = 8
}