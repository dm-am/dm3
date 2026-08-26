using System;
using System.Collections.Generic;
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
                // The owner check reads through a nullable author on purpose:
                // an NPC has none, and the NPC arm right below is the one that
                // decides such a post. Dereferencing it first turned posting
                // as an NPC into a null reference before the rule was reached.
                return access != null &&
                       GrantsWriting(access) &&
                       (access.Character.Author?.UserId == user.UserId ||
                        access.Character.IsNpc &&
                        room.Game.GetRoles(user.UserId).HasEditAccess());
            case RoomIntention.CreatePost:
                // Master/assistant can always post without character
                if (room.Game.GetRoles(user.UserId).HasEditAccess())
                {
                    return true;
                }
                // In Chat rooms, a reader the room grants writing to posts directly
                if (room.Type == RoomType.Chat)
                {
                    return MayWriteAsReader(room, user);
                }
                return false;
            default:
                return false;
        }
    }

    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, RoomToUpdate target) => intention switch
    {
        RoomIntention.CreatePostPendency => target.Accesses.Any(a => a.Character?.Author?.UserId == user.UserId) ||
                                            target.Game.GetRoles(user.UserId).HasEditAccess(),
        // Chat room history: master/assistant, or anybody the room was opened to
        RoomIntention.ViewMessages =>
            target.Type == RoomType.Chat &&
            (target.Game.GetRoles(user.UserId).HasEditAccess() ||
             MayReadAsReader(target, user)),
        // Writing in it takes the same seat plus the policy that admits writing
        RoomIntention.SendMessage =>
            target.Type == RoomType.Chat &&
            (target.Game.GetRoles(user.UserId).HasEditAccess() ||
             MayWriteAsReader(target, user)),
        _ => false
    };

    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, PostPendency target) => intention switch
    {
        RoomIntention.DeletePostPendency => target.CreatedBy.UserId == user.UserId,
        _ => false
    };

    /// <summary>
    /// The policy the row was granted with admits writing in the room.
    /// </summary>
    /// <remarks>
    /// The row is admission to the room, and the policy on it is the whole of what
    /// separates a seat in the audience from a voice. It was stored, validated and
    /// offered on the settings screen, and never once read here, so a ReadOnly grant
    /// wrote exactly as a Full one did, both as a character and as a reader.
    /// </remarks>
    private static bool GrantsWriting(RoomAccess access) => access.Policy == RoomAccessPolicy.Full;

    /// <summary>
    /// The reader rows of this user in this room, asked by three of the arms above.
    /// </summary>
    /// <remarks>
    /// It used to be spelled out twice and the two spellings had drifted apart: one
    /// dereferenced User, the other went through User?., so the same access row
    /// answered a NullReferenceException on one path and a denial on the other. The
    /// row's user comes from a projection and not from a schema constraint, so a row
    /// without one is reachable, and a row that names nobody grants nobody anything.
    /// </remarks>
    private static IEnumerable<RoomAccess> ReaderRows(RoomToUpdate room, IAuthorizationSubject user) =>
        room.Accesses.Where(a =>
            a.TargetType == RoomAccessTargetType.Reader &&
            a.User?.UserId == user.UserId);

    /// <summary>The room was opened to this user as a reader.</summary>
    private static bool MayReadAsReader(RoomToUpdate room, IAuthorizationSubject user) =>
        ReaderRows(room, user).Any();

    /// <summary>One of those rows also grants them writing.</summary>
    private static bool MayWriteAsReader(RoomToUpdate room, IAuthorizationSubject user) =>
        ReaderRows(room, user).Any(GrantsWriting);
}
