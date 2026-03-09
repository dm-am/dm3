using System;
using System.Linq;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.PostPendencies;
using DM.Domain.Game.Features.RoomAccesses;
using DM.Domain.Game.Features.Rooms;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc />
internal class RoomIntentionResolver :
    IIntentionResolver<RoomIntention, (RoomToUpdate, Guid?)>,
    IIntentionResolver<RoomIntention, RoomToUpdate>,
    IIntentionResolver<RoomIntention, PostPendency>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, (RoomToUpdate, Guid?) target)
    {
        var (room, characterId) = target;
        switch (intention)
        {
            case RoomIntention.CreatePost when characterId.HasValue:
                var access = room.Accesses.FirstOrDefault(a => a.Character?.Id == characterId.Value);
                return access != null &&
                       (access.Character.Author.UserId == user.UserId ||
                        access.Character.IsNpc &&
                        room.Game.GetRoles(user.UserId).HasEditAccess());
            case RoomIntention.CreatePost:
                // Master/assistant can always post without character
                if (room.Game.GetRoles(user.UserId).HasEditAccess())
                {
                    return true;
                }
                // In Chat rooms, users with Reader access can post directly
                if (room.Type == RoomType.Chat)
                {
                    return room.Accesses.Any(a =>
                        a.TargetType == RoomAccessTargetType.Reader &&
                        a.User.UserId == user.UserId);
                }
                return false;
            default:
                return false;
        }
    }

    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, RoomToUpdate target) => intention switch
    {
        RoomIntention.CreatePostPendency => target.Accesses.Any(a => a.Character?.Author.UserId == user.UserId) ||
                                            target.Game.GetRoles(user.UserId).HasEditAccess(),
        _ => false
    };

    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, PostPendency target) => intention switch
    {
        RoomIntention.DeletePostPendency => target.CreatedBy.UserId == user.UserId,
        _ => false
    };
}
