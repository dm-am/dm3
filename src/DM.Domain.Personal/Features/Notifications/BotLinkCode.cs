using System;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// Result of generating a bot linking code
/// </summary>
public class BotLinkCode
{
    /// <summary>How long a code is accepted: the service stamps it, the repository finds by it.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The verification code to send to the bot
    /// </summary>
    public string Code { get; set; } = null!;

    /// <summary>
    /// When the code expires (UTC)
    /// </summary>
    public DateTimeOffset ExpiresUtc { get; set; }
}
