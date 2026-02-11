using System;

namespace DM.Web.API.Dto.Notifications;

/// <summary>
/// Result of generating a bot linking code
/// </summary>
public class BotLinkResultDto
{
    /// <summary>
    /// Verification code to send to the bot
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// When the code expires
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
