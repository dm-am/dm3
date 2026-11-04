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
    /// <remarks>
    /// The game's blacklist is not asked: it closes the discussion and the application, not the rooms.
    /// </remarks>
    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, (RoomToUpdate, Guid?) target)
    {
        var (room, characterId) = target;
        switch (intention)
        {
            case RoomIntention.CreatePost when characterId.HasValue:
                var access = room.Accesses.FirstOrDefault(a => a.Character?.Id == characterId.Value);
                // An NPC has no author, and the NPC arm decides its post.
                return access is { Character: { } character } &&
                       GrantsWriting(access) &&
                       InPlay(access) &&
                       (character.Author?.UserId == user.UserId ||
                        character.IsNpc &&
                        room.Game.GetRoles(user.UserId).HasEditAccess());
            case RoomIntention.CreatePost:
                return room.Game.GetRoles(user.UserId).HasEditAccess() ||
                       room.Type == RoomType.Chat && MayWriteAsReader(room, user);
            default:
                return false;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The open arm of ViewMessages asks nothing about the game: the chat room reaches
    /// this overload only through RoomService.GetChatRoomAsync, whose RoomAvailable filter
    /// has already turned away a game the reader does not see.
    /// </remarks>
    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, RoomToUpdate target) => intention switch
    {
        // A player marks the turn through a character in play, the way he posts.
        RoomIntention.CreatePostPendency => target.Accesses.Any(a =>
                                                a.Character?.Author?.UserId == user.UserId && InPlay(a)) ||
                                            target.Game.GetRoles(user.UserId).HasEditAccess(),
        // Openness is about reading: a closed room is read by the leads and whoever it was opened to.
        RoomIntention.ViewMessages =>
            target.Type == RoomType.Chat &&
            (target.AccessType == RoomAccessType.Open ||
             target.Game.GetRoles(user.UserId).HasEditAccess() ||
             HoldsARow(target, user)),
        // Writing is the room's to give, open or closed alike.
        RoomIntention.SendMessage =>
            target.Type == RoomType.Chat &&
            (target.Game.GetRoles(user.UserId).HasEditAccess() ||
             MayWriteAsReader(target, user) ||
             MayWriteAsCharacter(target, user)),
        _ => false
    };

    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, PostPendency target) => intention switch
    {
        RoomIntention.DeletePostPendency => target.CreatedBy?.UserId == user.UserId,
        _ => false
    };

    /// <summary>
    /// The policy the row was granted with admits writing in the room.
    /// </summary>
    private static bool GrantsWriting(RoomAccess access) => access.Policy == RoomAccessPolicy.Full;

    /// <summary>
    /// The row's character is in play: accepted and not retired.
    /// </summary>
    /// <remarks>
    /// The access rows of a character stay with the room when it leaves the game,
    /// and the roster is what says it no longer plays.
    /// </remarks>
    private static bool InPlay(RoomAccess access) => access.Character?.Status == CharacterStatus.Active;

    /// <summary>
    /// The reader rows of this user in this room; the row's user comes from a projection, so it may be absent.
    /// </summary>
    private static IEnumerable<RoomAccess> ReaderRows(RoomToUpdate room, IAuthorizationSubject user) =>
        room.Accesses.Where(a =>
            a.TargetType == RoomAccessTargetType.Reader &&
            a.User?.UserId == user.UserId);

    /// <summary>
    /// The rows of this user's own characters in this room, whatever became of the
    /// characters since.
    /// </summary>
    /// <remarks>
    /// Matched on the character's author, as posting matches it: the row's User holds it only by projection.
    /// </remarks>
    private static IEnumerable<RoomAccess> CharacterRows(RoomToUpdate room, IAuthorizationSubject user) =>
        room.Accesses.Where(a =>
            a.TargetType == RoomAccessTargetType.Character &&
            a.Character?.Author?.UserId == user.UserId);

    /// <summary>
    /// The room was opened to this user: as a reader, or through a character of
    /// theirs.
    /// </summary>
    private static bool HoldsARow(RoomToUpdate room, IAuthorizationSubject user) =>
        ReaderRows(room, user).Any() || CharacterRows(room, user).Any();

    /// <summary>One of the reader rows also grants them writing.</summary>
    private static bool MayWriteAsReader(RoomToUpdate room, IAuthorizationSubject user) =>
        ReaderRows(room, user).Any(GrantsWriting);

    /// <summary>
    /// A character of theirs in play holds a row that grants writing: the same
    /// three conditions a post spoken by that character is held to.
    /// </summary>
    private static bool MayWriteAsCharacter(RoomToUpdate room, IAuthorizationSubject user) =>
        CharacterRows(room, user).Any(a => GrantsWriting(a) && InPlay(a));
}
