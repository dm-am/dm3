using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;

namespace DM.Web.API.Shared.Comments;

/// <summary>
/// Reading a discussion, written once for every comment surface.
/// </summary>
/// <remarks>
/// Hiding the authors the reader blacklisted is one behaviour across blogs,
/// publications, topics and games; DiscussionOwnershipShould keeps a second copy
/// from appearing.
/// </remarks>
internal static class CommentReading
{
    /// <summary>
    /// Ids of the authors this reader chose to hide, or null when there is
    /// nothing to exclude: the domain queries read null as "no filter".
    /// </summary>
    public static async Task<IReadOnlyCollection<Guid>?> HiddenAuthorsAsync(
        IUserBlacklistChecker blacklistChecker, IIdentity identity)
    {
        if (identity.User?.IsAuthenticated != true)
        {
            return null;
        }

        var blockedIds = await blacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(
            identity.User.UserId, UserBlacklistSettings.HideComments);

        return blockedIds.Count > 0 ? blockedIds : null;
    }
}
