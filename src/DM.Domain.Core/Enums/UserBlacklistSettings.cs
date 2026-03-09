using System;

namespace DM.Domain.Core.Enums;

/// <summary>
/// User blacklist behavior settings (flags)
/// </summary>
[Flags]
public enum UserBlacklistSettings
{
    /// <summary>
    /// No special behavior
    /// </summary>
    None = 0,

    /// <summary>
    /// Hide comments from blocked users
    /// </summary>
    HideComments = 1 << 0,

    /// <summary>
    /// Hide messages in global chat from blocked users
    /// </summary>
    HideMessages = 1 << 1,

    /// <summary>
    /// Hide their games from sidebars/listings
    /// </summary>
    HideGames = 1 << 2,

    /// <summary>
    /// Hide their blogs from sidebars/listings (future use)
    /// </summary>
    HideBlogs = 1 << 3,

    /// <summary>
    /// Block direct messages from blocked users
    /// </summary>
    BlockDirectMessages = 1 << 4,

    /// <summary>
    /// Auto-populate game/blog blacklists from personal blacklist when creating new content
    /// </summary>
    AutoPopulateContentBlacklist = 1 << 5,

    /// <summary>
    /// Default settings for new blacklist entries
    /// </summary>
    Default = HideComments | HideMessages | BlockDirectMessages
}
