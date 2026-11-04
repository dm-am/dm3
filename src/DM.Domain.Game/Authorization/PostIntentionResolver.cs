using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc />
internal class PostIntentionResolver :
    IIntentionResolver<PostIntention, Post>,
    IIntentionResolver<PostIntention, (Post, RoomToUpdate)>
{
    private readonly IPrivateBlockReading _privateBlockReading;

    public PostIntentionResolver(IPrivateBlockReading privateBlockReading) =>
        _privateBlockReading = privateBlockReading;

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PostIntention intention, Post target)
    {
        return intention switch
        {
            // The author's edit window is the client's affordance, as for comments and post reviews.
            // Nothing in a game room is moderated, so no rank deletes a post of somebody else.
            PostIntention.Delete => target.Author?.UserId == user.UserId,
            // The leads as the post read names them, for a read that has no room at hand.
            PostIntention.EditText => MayEditText(user, target, target.GameLeadUserIds.Contains(user.UserId)),
            _ => false
        };
    }

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PostIntention intention, (Post, RoomToUpdate) target)
    {
        var (post, room) = target;
        return intention switch
        {
            PostIntention.EditText => MayEditText(user, post, room.Game.GetRoles(user.UserId).HasEditAccess()),
            PostIntention.EditCharacter => post.Author?.UserId == user.UserId ||
                                           room.Game.GetRoles(user.UserId).HasEditAccess() &&
                                           (post.Character == null || post.Character.IsNpc),
            _ => false
        };
    }

    /// <summary>
    /// The author edits the text; the leads edit it subject to the character's
    /// post-edit access policy. Moderation does not: nothing in a game room is
    /// moderated. Nobody but the author edits a text whose hidden blocks and marked
    /// files he does not all read.
    /// </summary>
    /// <remarks>
    /// The editor is handed the text as stored and saves what it holds, so an edit
    /// is a read of every block and of the address of every file in it, and a text
    /// handed over without that would be saved without it. A lead reads every block
    /// and every file.
    /// </remarks>
    private bool MayEditText(IAuthorizationSubject user, Post post, bool leadsTheGame) =>
        post.Author?.UserId == user.UserId ||
        leadsTheGame &&
        (post.Character == null || post.Character.IsNpc ||
         post.Character.AccessPolicy.HasFlag(CharacterAccessPolicy.PostEditAllowed)) &&
        _privateBlockReading.ReadsEveryBlock(user, post.GameTextPrivateBlocks()) &&
        PostAttachmentVisibility.HiddenFrom(_privateBlockReading, user, post).Count == 0;
}
