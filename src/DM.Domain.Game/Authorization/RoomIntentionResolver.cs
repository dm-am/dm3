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
                    return HasReaderAccess(room, user);
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
        // Chat room message permissions: master/assistant or users with Reader access
        RoomIntention.ViewMessages or RoomIntention.SendMessage =>
            target.Type == RoomType.Chat &&
            (target.Game.GetRoles(user.UserId).HasEditAccess() ||
             HasReaderAccess(target, user)),
        _ => false
    };

    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, PostPendency target) => intention switch
    {
        RoomIntention.DeletePostPendency => target.CreatedBy.UserId == user.UserId,
        _ => false
    };

    /// <summary>
    /// Reader access to the room, asked by two of the overloads above.
    /// </summary>
    /// <remarks>
    /// It used to be spelled out twice and the two spellings had drifted apart: one
    /// dereferenced User, the other went through User?., so the same access row
    /// answered a NullReferenceException on one path and a denial on the other. The
    /// row's user comes from a projection and not from a schema constraint, so a row
    /// without one is reachable, and a row that names nobody grants nobody anything.
    /// </remarks>
    private static bool HasReaderAccess(RoomToUpdate room, IAuthorizationSubject user) =>
        room.Accesses.Any(a =>
            a.TargetType == RoomAccessTargetType.Reader &&
            a.User?.UserId == user.UserId);
}
