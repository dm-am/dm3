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
            PostIntention.Delete => target.Author.UserId == user.UserId,
            _ => false
        };
    }

    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, PostIntention intention, (Post, RoomToUpdate) target)
    {
        var (post, room) = target;
        return intention switch
        {
            PostIntention.EditText => post.Author.UserId == user.UserId ||
                                      room.Game.GetRoles(user.UserId).HasEditAccess() &&
                                      (post.Character == null || post.Character.IsNpc ||
                                       post.Character.AccessPolicy.HasFlag(CharacterAccessPolicy.PostEditAllowed)),
            PostIntention.EditCharacter => post.Author.UserId == user.UserId ||
                                           room.Game.GetRoles(user.UserId).HasEditAccess() &&
                                           (post.Character == null || post.Character.IsNpc),
            PostIntention.EditMasterMessage => room.Game.GetRoles(user.UserId).HasEditAccess() &&
                                               (post.Character == null || post.Character.IsNpc),
            _ => false
        };
    }
}
