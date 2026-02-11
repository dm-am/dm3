using System;

namespace DM.Services.Community.BusinessProcesses.Account.BotLink;

/// <summary>
/// Result of generating a bot linking code
/// </summary>
public class BotLinkCode
{
    /// <summary>
    /// The verification code to send to the bot
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// When the code expires
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
