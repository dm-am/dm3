using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Blacklists;

/// <summary>
/// Partial update of blacklist behavior settings
/// </summary>
/// <remarks>
/// Every flag is optional: an omitted one keeps its current value. The read
/// model cannot be reused here — its flags are plain booleans, so an omitted
/// one is indistinguishable from an explicit false and a request carrying one
/// flag would silently clear the other five.
/// </remarks>
public class UpdateBlacklistSettingsRequest
{
    /// <summary>Hide comments from blocked users</summary>
    public bool? HideComments { get; set; }

    /// <summary>Hide messages in global chat from blocked users</summary>
    public bool? HideMessages { get; set; }

    /// <summary>Hide games from blocked users in listings</summary>
    public bool? HideGames { get; set; }

    /// <summary>Hide blogs from blocked users in listings</summary>
    public bool? HideBlogs { get; set; }

    /// <summary>
    /// Block private correspondence from blocked users: a direct chat, a group
    /// chat of two, and being added to a group chat by them at all
    /// </summary>
    public bool? BlockDirectMessages { get; set; }

    /// <summary>Auto-populate game/blog blacklists from personal blacklist when creating new content</summary>
    public bool? AutoPopulateContentBlacklist { get; set; }

    /// <summary>
    /// Fold this request onto the settings the user has now.
    /// </summary>
    internal UserBlacklistSettings ApplyTo(UserBlacklistSettings current)
    {
        var result = current;
        Set(ref result, UserBlacklistSettings.HideComments, HideComments);
        Set(ref result, UserBlacklistSettings.HideMessages, HideMessages);
        Set(ref result, UserBlacklistSettings.HideGames, HideGames);
        Set(ref result, UserBlacklistSettings.HideBlogs, HideBlogs);
        Set(ref result, UserBlacklistSettings.BlockDirectMessages, BlockDirectMessages);
        Set(ref result, UserBlacklistSettings.AutoPopulateContentBlacklist, AutoPopulateContentBlacklist);
        return result;
    }

    private static void Set(ref UserBlacklistSettings flags, UserBlacklistSettings flag, bool? value)
    {
        if (value == true)
        {
            flags |= flag;
        }
        else if (value == false)
        {
            flags &= ~flag;
        }
    }
}
