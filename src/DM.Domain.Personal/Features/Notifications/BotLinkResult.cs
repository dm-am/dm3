using System;

namespace DM.Domain.Personal.Features.Notifications;

/// <summary>
/// Result of verifying a bot linking code
/// </summary>
public class BotLinkResult
{
    /// <summary>
    /// Whether the linking was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Username (returned on success for bot confirmation message)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Error message (returned on failure)
    /// </summary>
    public string? Error { get; set; }
}
