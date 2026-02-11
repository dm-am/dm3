using System;

namespace DM.Services.Community.BusinessProcesses.Account.BotLink;

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
    /// User login (returned on success for bot confirmation message)
    /// </summary>
    public string? UserLogin { get; set; }

    /// <summary>
    /// Error message (returned on failure)
    /// </summary>
    public string? Error { get; set; }
}
