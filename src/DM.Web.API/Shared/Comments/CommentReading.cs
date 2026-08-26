using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.BbRendering;
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
        DiscussionMapper mapper)
    {
        // Through AnonymousIdentity rather than the empty Guid: a visitor who
        // is nobody carries the empty id, and so does a field nobody filled in,
        // so the two compare equal and a guest is answered "you wrote this".
        // A projection that forgot to fill the author is not hypothetical - it
        // is what once handed the game master's sight of every [private] block
        // to anonymous readers.
        var currentUserId = AnonymousIdentity.Of(identity.User);
        var isAuthenticated = identity.User?.IsAuthenticated ?? false;
        var isModerator = (identity.User?.Role ?? UserRole.Guest) >= UserRole.Moderator;

        var discussionComments = comments.Select(c =>
        {
            var dc = mapper.ToDiscussionComment(c);
            // Both comparisons are guarded by the same pattern: with no
            // reader there is nobody to be the author of anything and nobody
            // whose like to find, and null == null must not answer otherwise.
            var isAuthor = currentUserId is { } readerId && c.Author?.UserId == readerId;
            dc.IsLikedByMe = currentUserId is { } liker &&
                             (c.Likes?.Any(l => l.UserId == liker) ?? false);
            dc.CanEdit = isAuthor || isModerator;
            dc.CanDelete = isAuthor || isModerator;
            dc.CanLike = isAuthenticated && !isAuthor;
            return dc;
        }).ToList();

        return new DiscussionResponse(
            discussionComments,
            new PagingInfo(paging),
            discussionComments.Sum(c => c.LikesCount),
            isAuthenticated);
    }

    /// <summary>
    /// One domain comment as the source of a quotation of it.
    /// </summary>
    /// <remarks>
    /// Here for the same reason the two above are: four surfaces own their
    /// comments and their permissions, and the shape of a quotation is not one
    /// of the things they own. Outside a game the header carries the login, so
    /// there is nothing per-surface left to decide.
    ///
    /// The caller has already read the comment through the service that
    /// authorizes reading it. Nothing here checks permissions, and nothing here
    /// should: a second rule beside the first is a second rule to keep in step.
    /// </remarks>
    public static Envelope<QuoteSource> ToQuote(
        DomainComment comment,
        CommentMapper mapper,
        IQuoteSourceService quoteSourceService) =>
        quoteSourceService.Build(mapper.ToComment(comment).Text, comment.Author?.Username);
}
