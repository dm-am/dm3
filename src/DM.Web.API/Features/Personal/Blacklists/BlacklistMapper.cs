using DM.Domain.Core.Enums;
using Riok.Mapperly.Abstractions;
using DomainBlacklistEntry = DM.Domain.Personal.Features.Blacklists.BlacklistEntry;

namespace DM.Web.API.Features.Personal.Blacklists;

/// <summary>
/// Compile-time mapper for the user blacklist
/// </summary>
[Mapper]
public partial class BlacklistMapper
{
    /// <summary>
    /// Domain blacklist entry to its response DTO
    /// </summary>
    public partial BlacklistEntry ToBlacklistEntry(DomainBlacklistEntry entry);

    /// <summary>
    /// Settings flags to the response DTO. No reverse map: folding a request onto
    /// the flags needs their current value (UpdateBlacklistSettingsRequest.ApplyTo).
    /// </summary>
    public static BlacklistSettings ToBlacklistSettings(UserBlacklistSettings flags) => new()
    {
        HideComments = flags.HasFlag(UserBlacklistSettings.HideComments),
        HideMessages = flags.HasFlag(UserBlacklistSettings.HideMessages),
        HideGames = flags.HasFlag(UserBlacklistSettings.HideGames),
        HideBlogs = flags.HasFlag(UserBlacklistSettings.HideBlogs),
        BlockDirectMessages = flags.HasFlag(UserBlacklistSettings.BlockDirectMessages),
        AutoPopulateContentBlacklist = flags.HasFlag(UserBlacklistSettings.AutoPopulateContentBlacklist)
    };
}
