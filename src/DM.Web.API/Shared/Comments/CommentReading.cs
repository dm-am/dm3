using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.Dto;
using DomainComment = DM.Domain.Core.Comments.Comment;

namespace DM.Web.API.Shared.Comments;

/// <summary>
/// Reading a discussion, written once for every comment surface.
/// </summary>
/// <remarks>
/// Blogs, publications, topics and games each own their comments, their
/// permissions and their query type — that part stays four things. The read
/// path is one behaviour: hide the authors the reader blacklisted, then say,
/// for every comment, whether he may edit, delete or like it. It was copied per
/// surface, and the copies had already parted — the publication one shipped
/// without the blacklist filter, so a reader who hid an author saw him again
/// under a publication and nowhere else, with the endpoint answering 200 and
/// one comment too many.
///
/// A fifth surface now gets both halves or neither, and
/// DiscussionOwnershipShould keeps a sixth copy from appearing.
/// </remarks>
internal static class CommentReading
{
    /// <summary>
    /// Ids of the authors this reader chose to hide, or null when there is
    /// nothing to exclude.
    /// </summary>
    /// <remarks>
    /// Null rather than an empty collection: the domain queries treat "no
    /// filter" and "an empty filter" alike, and every call site has always
    /// passed null for the guest and for the reader with an empty list.
    /// </remarks>
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

    /// <summary>
    /// A page of domain comments as the discussion response: what this reader
    /// may do with each comment, the likes on the page, and whether he may add
    /// a comment at all.
    /// </summary>
    public static DiscussionResponse ToDiscussion(
        IEnumerable<DomainComment> comments,
        PagingResult paging,
        IIdentity identity,
        IMapper mapper)
    {
        var currentUserId = identity.User?.UserId ?? Guid.Empty;
        var isAuthenticated = identity.User?.IsAuthenticated ?? false;
        var isModerator = (identity.User?.Role ?? UserRole.Guest) >= UserRole.Moderator;

        var discussionComments = comments.Select(c =>
        {
            var dc = mapper.Map<DiscussionComment>(c);
            var isAuthor = c.Author?.UserId == currentUserId;
            dc.IsLikedByMe = c.Likes?.Any(l => l.UserId == currentUserId) ?? false;
            dc.CanEdit = isAuthor || isModerator;
            dc.CanDelete = isAuthor || isModerator;
            dc.CanLike = isAuthenticated && !isAuthor;
            return dc;
        }).ToList();

        return new DiscussionResponse(
            discussionComments,
            new Paging(paging),
            discussionComments.Sum(c => c.LikesCount),
            isAuthenticated);
    }
}
