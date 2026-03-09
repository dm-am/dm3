namespace DM.Web.API.Features.Personal.Blacklists;

/// <summary>
/// Blacklist behavior settings
/// </summary>
public class BlacklistSettings
{
    /// <summary>Hide comments from blocked users</summary>
    public bool HideComments { get; set; }

    /// <summary>Hide messages in global chat from blocked users</summary>
    public bool HideMessages { get; set; }

    /// <summary>Hide games from blocked users in listings</summary>
    public bool HideGames { get; set; }

    /// <summary>Hide blogs from blocked users in listings</summary>
    public bool HideBlogs { get; set; }

    /// <summary>Block direct messages from blocked users</summary>
    public bool BlockDirectMessages { get; set; }

    /// <summary>Auto-populate game/blog blacklists from personal blacklist when creating new content</summary>
    public bool AutoPopulateContentBlacklist { get; set; }
}
