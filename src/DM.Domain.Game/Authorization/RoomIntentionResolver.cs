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
    /// Who writes in a room is decided by the room and the roster and by nothing
    /// else: the access row with its policy, the character being in play, the
    /// lead's role. The game's blacklist is not asked; it closes the
    /// discussion and the application, not the rooms.
    /// </remarks>
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
                       InPlay(access) &&
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

    /// <inheritdoc />
    /// <remarks>
    /// The open arm of ViewMessages asks nothing about the game, and it does not
    /// have to: a chat room reaches this overload only through
    /// RoomService.GetChatRoomAsync, which reads it with RoomAvailable, and that
    /// filter has already turned away a room of a game the reader does not see
    /// (held end to end by ChatRoomMessagesShould,
    /// KeepTheOpenChatRoomOfAGameAStrangerDoesNotSeeShut). The same filter
    /// admits every row of the reader's characters, whatever the policy and
    /// whatever became of the character, and reading here answers the same, so
    /// for every reader but one the room page and its menu row never open on a
    /// history that refuses him. The one is the judge of premoderation, who is
    /// let into a game that waits for his verdict by his rank: the menu and the
    /// room page are read with the rank (GetAllVisible, GetAvailable) and the
    /// history without it (GetForUpdate), so he finds the chat room in the menu,
    /// opens its page and gets 404 for its messages, as he did before the open
    /// arm. The chat used to ask reader rows alone and never the room's access
    /// type: an open chat room refused every player, and a closed one refused
    /// the players its character rows had let in.
    /// </remarks>
    public bool IsAllowed(IAuthorizationSubject user, RoomIntention intention, RoomToUpdate target) => intention switch
    {
        // A player marks the turn through a character in play, the way he posts.
        RoomIntention.CreatePostPendency => target.Accesses.Any(a =>
                                                a.Character?.Author?.UserId == user.UserId && InPlay(a)) ||
                                            target.Game.GetRoles(user.UserId).HasEditAccess(),
        // Chat room history. Openness is about reading: an open room is read by
        // everybody who sees the game, a closed one by the leads and by whoever
        // it was opened to, as a reader or through a character.
        RoomIntention.ViewMessages =>
            target.Type == RoomType.Chat &&
            (target.AccessType == RoomAccessType.Open ||
             target.Game.GetRoles(user.UserId).HasEditAccess() ||
             HoldsARow(target, user)),
        // Writing in it is the room's to give, open or closed alike: the leads,
        // and the people it was opened to with the policy that admits writing.
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
    /// <remarks>
    /// The row is admission to the room, and the policy on it is the whole of what
    /// separates a seat in the audience from a voice. It was stored, validated and
    /// offered on the settings screen, and never once read here, so a ReadOnly grant
    /// wrote exactly as a Full one did, both as a character and as a reader.
    /// </remarks>
    private static bool GrantsWriting(RoomAccess access) => access.Policy == RoomAccessPolicy.Full;

    /// <summary>
    /// The row's character is in play: accepted and not retired.
    /// </summary>
    /// <remarks>
    /// The access rows of a character stay with the room when it leaves the game
    /// - dead, exiled, or walked away - and the roster is what says it no longer
    /// plays. The composer only ever offered active characters, while the server
    /// asked the row alone, so a player who had left kept writing through the
    /// rows of his retired character to anybody who sent the request by hand.
    /// The game's blacklist used to stand in for this rule for the one person it
    /// held.
    /// </remarks>
    private static bool InPlay(RoomAccess access) => access.Character?.Status == CharacterStatus.Active;

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

    /// <summary>
    /// The rows of this user's own characters in this room, whatever became of the
    /// characters since.
    /// </summary>
    /// <remarks>
    /// Matched on the character's author, the way the posting and the pendency
    /// arms match it, and not on the row's User: that field holds the author for
    /// a character row too, but only because a projection put it there.
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
