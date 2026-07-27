using DM.Domain.Core.Authorization;
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
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PostIntention intention, Post target)
    {
        return intention switch
        {
            // Author or global moderator+ (doc 4.2.2.12: "moderator+ anytime").
            // The 15-minute author window is a client-side affordance, matching
            // the Comment / PostReview delete resolvers.
            PostIntention.Delete => target.Author.UserId == user.UserId ||
                                    user.Role >= UserRole.Moderator,
            _ => false
        };
    }

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PostIntention intention, (Post, RoomToUpdate) target)
    {
        var (post, room) = target;
        return intention switch
        {
            // Author or global moderator+ edit anytime (doc 4.2.2.12), plus
            // game leads (master/assistant) subject to the character's
            // post-edit access policy. The 15-minute author window is a
            // client-side affordance.
            PostIntention.EditText => post.Author.UserId == user.UserId ||
                                      user.Role >= UserRole.Moderator ||
                                      room.Game.GetRoles(user.UserId).HasEditAccess() &&
                                      (post.Character == null || post.Character.IsNpc ||
                                       post.Character.AccessPolicy.HasFlag(CharacterAccessPolicy.PostEditAllowed)),
            PostIntention.EditCharacter => post.Author.UserId == user.UserId ||
                                           room.Game.GetRoles(user.UserId).HasEditAccess() &&
                                           (post.Character == null || post.Character.IsNpc),
            _ => false
        };
    }
}
