namespace DM.Web.API.Dto.Notifications;

/// <summary>
/// Request to verify a bot linking code (from bot to API)
/// </summary>
public class VerifyBotLinkRequest
{
    /// <summary>
    /// Verification code entered by user
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// Channel type: "discord" or "telegram"
    /// </summary>
    public string ChannelType { get; set; } = null!;

    /// <summary>
    /// External platform user/chat ID
    /// </summary>
    public string ExternalId { get; set; } = null!;
}
