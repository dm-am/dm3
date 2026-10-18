using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

/// <summary>
/// Gates who may speak in a room and as whom. The expensive mistake here is not
/// a locked door but an open one: posting under another player's character puts
/// words in their mouth, and reading a chat room hands over private history.
/// </summary>
public class RoomIntentionResolverShould : UnitTestBase
{
    private readonly RoomIntentionResolver resolver = new();

    private static readonly Guid MasterId = Guid.NewGuid();
    private static readonly Guid AssistantId = Guid.NewGuid();
    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static GameDetails GameLedBy(Guid masterId, Guid? assistantId = null)
    {
        var builder = new GameBuilder().WithMaster(masterId);
        return assistantId.HasValue
            ? builder.WithAssistants(assistantId.Value).Please()
            : builder.Please();
    }

    #region The room and the roster decide, not the blacklist

    /// <summary>
    /// The game's blacklist closes the discussion and the application, not the
    /// rooms: a seat the room gives is a seat, whoever the list holds.
    /// </summary>
    [Fact]
    public void LetABlacklistedUserWriteThroughTheSeatsTheRoomGivesHim()
    {
        var characterId = Guid.NewGuid();
        var game = new GameBuilder().WithMaster(MasterId).WithBlacklisted(PlayerId).Please();
        var room = new RoomBuilder()
            .WithGame(game)
            .WithType(RoomType.Chat)
            .WithCharacterAccess(characterId, PlayerId)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.CreatePostPendency, room).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
    }

    /// <summary>
    /// A character out of play writes nothing through the rows it still has in
    /// the room: the rows stay when it leaves, is exiled or dies, and the roster
    /// is what says it no longer plays. The composer never offered it; the server
    /// asked the row alone.
    /// </summary>
    [Theory]
    [InlineData(CharacterStatus.Retired)]
    [InlineData(CharacterStatus.UnderReview)]
    [InlineData(CharacterStatus.Declined)]
    public void KeepACharacterOutOfPlayFromWritingInTheRoom(CharacterStatus status)
    {
        var characterId = Guid.NewGuid();
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(characterId, PlayerId, status: status)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeFalse();
        resolver.IsAllowed(user, RoomIntention.CreatePostPendency, room).Should().BeFalse();
    }

    /// <summary>
    /// The same for a lead posting as an NPC that is out of play.
    /// </summary>
    [Fact]
    public void KeepAnNpcOutOfPlayFromWritingInTheRoom()
    {
        var npcId = Guid.NewGuid();
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(npcId, Guid.Empty, isNpc: true, status: CharacterStatus.Retired)
            .Please();
        var master = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(master, RoomIntention.CreatePost, (room, (Guid?)npcId)).Should().BeFalse();
    }

    #endregion

    #region Posting as a character

    [Fact]
    public void LetAPlayerPostAsTheirOwnCharacter()
    {
        var characterId = Guid.NewGuid();
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(characterId, PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeTrue();
    }

    [Fact]
    public void NotLetAPlayerPostAsSomebodyElsesCharacter()
    {
        var characterId = Guid.NewGuid();
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(characterId, PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeFalse();
    }

    [Fact]
    public void NotLetTheMasterPostAsAPlayersCharacter()
    {
        var characterId = Guid.NewGuid();
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(characterId, PlayerId)
            .Please();
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        // Running the game is not the same as speaking for a player. The NPC
        // exemption below is the whole of what leading the game buys here.
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LetGameLeadsPostAsAnNpc(bool asAssistant)
    {
        var characterId = Guid.NewGuid();
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId, AssistantId))
            .WithCharacterAccess(characterId, MasterId, isNpc: true)
            .Please();
        var user = Create.User(asAssistant ? AssistantId : MasterId)
            .WithRole(UserRole.RegularUser)
            .Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeTrue();
    }

    [Fact]
    public void NotLetAPlayerPostAsAnNpc()
    {
        var characterId = Guid.NewGuid();
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(characterId, MasterId, isNpc: true)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeFalse();
    }

    [Fact]
    public void NotLetACharacterPostIntoARoomItHasNoAccessTo()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(Guid.NewGuid(), PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        // The character belongs to the player, but this room was never opened to
        // it: the access row, not the ownership, is what admits a character.
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)Guid.NewGuid())).Should().BeFalse();
    }

    /// <summary>
    /// An open room is open to read, not to write: a character still needs an
    /// access of its own to post in it. The room's composer offers "От лица"
    /// only the characters with such an access (GameRoom.vue mirrors this arm),
    /// so the day this changes, the composer changes with it.
    /// </summary>
    [Fact]
    public void NotLetACharacterWithoutAnAccessPostEvenInAnOpenRoom()
    {
        var characterId = Guid.NewGuid();
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithAccessType(RoomAccessType.Open)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeFalse();
    }

    [Fact]
    public void NotLetAPlayerPostAsACharacterGrantedReadOnly()
    {
        var characterId = Guid.NewGuid();
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(characterId, PlayerId, policy: RoomAccessPolicy.ReadOnly)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        // The row admits the character to the room, the policy on it says whether
        // the character may write there. ReadOnly is a seat in the audience, and it
        // used to hand out a voice along with the seat.
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeFalse();
    }

    #endregion

    #region Posting without a character

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LetGameLeadsPostWithoutACharacter(bool asAssistant)
    {
        var room = new RoomBuilder().WithGame(GameLedBy(MasterId, AssistantId)).Please();
        var user = Create.User(asAssistant ? AssistantId : MasterId)
            .WithRole(UserRole.RegularUser)
            .Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeTrue();
    }

    [Fact]
    public void NotLetAPlayerPostWithoutACharacterInAnOrdinaryRoom()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(Guid.NewGuid(), PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        // In a game room every post is spoken by a character. Only a chat room
        // takes an unattributed post, and only from a reader.
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeFalse();
    }

    [Fact]
    public void LetAReaderPostInAChatRoomWithoutACharacter()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeTrue();
    }

    [Fact]
    public void NotLetAReaderPostWithoutACharacterInAnOrdinaryRoom()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeFalse();
    }

    [Fact]
    public void NotLetAStrangerPostInAChatRoom()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeFalse();
    }

    [Fact]
    public void NotLetACharacterAccessRowStandInForAReaderRowInAChatRoom()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithCharacterAccess(Guid.NewGuid(), PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        // A character row also carries the author in User. The branch is
        // deliberately narrowed to reader rows, so the author of a character
        // present in a chat room still needs a reader row of their own to post
        // without that character. A message is a different intention: the
        // character row admits it (see the chat room history below).
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeFalse();
    }

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    [InlineData(RoomIntention.CreatePostPendency)]
    [InlineData(RoomIntention.DeletePostPendency)]
    public void RefuseEveryIntentionOtherThanCreatePostOnTheCharacterOverload(RoomIntention intention)
    {
        var room = new RoomBuilder().WithGame(GameLedBy(MasterId)).Please();
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        // This overload answers one question only. Anything else routed into it
        // must fall through rather than inherit the master's edit access.
        resolver.IsAllowed(user, intention, (room, (Guid?)null)).Should().BeFalse();
    }

    #endregion

    #region Chat room history

    // The rooms below are closed unless a case says otherwise: in an open room
    // anybody reads, so only a closed one shows what a row admits to reading.

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void LetAReaderIntoAChatRoom(RoomIntention intention)
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Private)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, room).Should().BeTrue();
    }

    [Fact]
    public void LetAReaderGrantedReadOnlyReadAChatRoomAndNotWriteInIt()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Private)
            .WithReaderAccess(PlayerId, RoomAccessPolicy.ReadOnly)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        // Admission to the room and a voice in it are two grants, and the policy on
        // the row is the whole of what separates them. Both ways of writing into a
        // chat room are held to it: the message and the unattributed post.
        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeFalse();
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeFalse();
    }

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void LetGameLeadsIntoAChatRoomWithoutAnAccessRow(RoomIntention intention)
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId, AssistantId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Private)
            .Please();
        var user = Create.User(AssistantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, room).Should().BeTrue();
    }

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void KeepAStrangerOutOfAClosedChatRoom(RoomIntention intention)
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Private)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, room).Should().BeFalse();
    }

    /// <summary>
    /// A character the room was opened to reads it and, with a policy that admits
    /// writing, writes in it, the same in an open and in a closed room. The chat
    /// used to ask reader rows alone, so the player of a character holding Full
    /// in a closed chat room was refused both the history and the message, while
    /// the room page and its menu row let him in.
    /// </summary>
    [Theory]
    [InlineData(RoomAccessType.Open)]
    [InlineData(RoomAccessType.Private)]
    public void LetACharacterTheChatRoomWasOpenedToReadAndWriteInIt(RoomAccessType access)
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(access)
            .WithCharacterAccess(Guid.NewGuid(), PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeTrue();
    }

    [Theory]
    [InlineData(RoomAccessType.Open)]
    [InlineData(RoomAccessType.Private)]
    public void LetACharacterGrantedReadOnlyReadAChatRoomAndNotWriteInIt(RoomAccessType access)
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(access)
            .WithCharacterAccess(Guid.NewGuid(), PlayerId, policy: RoomAccessPolicy.ReadOnly)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeFalse();
    }

    /// <summary>
    /// A character out of play writes nothing in a chat room either, the way it
    /// posts nothing: its rows stay with the room when it leaves the game. They
    /// still open the room to read, as they open its page and its menu row: the
    /// room reaches the resolver through RoomAvailable, which admits every row of
    /// the reader's characters, and the page and its history must not disagree.
    /// </summary>
    [Theory]
    [InlineData(CharacterStatus.Retired)]
    [InlineData(CharacterStatus.UnderReview)]
    [InlineData(CharacterStatus.Declined)]
    public void KeepACharacterOutOfPlayFromWritingInAChatRoom(CharacterStatus status)
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Private)
            .WithCharacterAccess(Guid.NewGuid(), PlayerId, status: status)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeFalse();
    }

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void NotLetSomebodyElsesCharacterAdmitAStrangerToAClosedChatRoom(RoomIntention intention)
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Private)
            .WithCharacterAccess(Guid.NewGuid(), PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        // The row admits the author of its character and nobody else.
        resolver.IsAllowed(user, intention, room).Should().BeFalse();
    }

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void RefuseChatIntentionsOnAnOrdinaryRoomEvenForTheMaster(RoomIntention intention)
    {
        var room = new RoomBuilder().WithGame(GameLedBy(MasterId)).Please();
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        // The room type is checked before the roles are, so the messaging
        // intentions cannot be used to reach a game room's posts.
        resolver.IsAllowed(user, intention, room).Should().BeFalse();
    }

    [Fact]
    public void NotLetAnAdminIntoAChatRoomTheyHaveNoPartIn()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Private)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        // Room access is a game role question, not a site role one: no site rank
        // opens a private room.
        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeFalse();
    }

    #endregion

    #region An open chat room is open to read, not to write

    /// <summary>
    /// An open chat room is read by everybody who sees the game, like the posts
    /// of an open room. The resolver does not ask whether the game is visible:
    /// the room reaches it only through RoomAvailable, which has already asked.
    /// </summary>
    [Fact]
    public void LetAnybodyWhoSeesTheGameReadAnOpenChatRoom()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Open)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
    }

    /// <summary>
    /// Writing in it is the room's to give, open or closed: a player of the game
    /// whose character the room did not add reads it and does not write in it.
    /// Taking part in the game is not the grant, the row is.
    /// </summary>
    [Fact]
    public void NotLetAPlayerTheOpenChatRoomDidNotAddWriteInIt()
    {
        var game = new GameBuilder().WithMaster(MasterId).WithPlayers(PlayerId, StrangerId).Please();
        var room = new RoomBuilder()
            .WithGame(game)
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Open)
            .WithCharacterAccess(Guid.NewGuid(), PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeFalse();
    }

    /// <summary>
    /// Nor does an open room give a voice to a site rank: openness is about
    /// reading for an admin as much as for anybody else.
    /// </summary>
    [Fact]
    public void NotLetAnAdminWriteInAnOpenChatRoomTheyHaveNoPartIn()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Open)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeFalse();
    }

    #endregion

    #region Post pendencies

    [Fact]
    public void LetAPlayerWithACharacterInTheRoomRaiseAPendency()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithCharacterAccess(Guid.NewGuid(), PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePostPendency, room).Should().BeTrue();
    }

    [Fact]
    public void LetTheMasterRaiseAPendencyWithoutACharacterInTheRoom()
    {
        var room = new RoomBuilder().WithGame(GameLedBy(MasterId)).Please();
        var user = Create.User(MasterId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.CreatePostPendency, room).Should().BeTrue();
    }

    [Fact]
    public void NotLetAReaderRaiseAPendency()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        // A pendency says whose turn it is to write. Watching the room is not
        // taking part in it.
        resolver.IsAllowed(user, RoomIntention.CreatePostPendency, room).Should().BeFalse();
    }

    [Fact]
    public void LetTheAuthorOfAPendencyDeleteIt()
    {
        var pendency = new PostPendency { CreatedBy = new GeneralUser { UserId = PlayerId } };
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.DeletePostPendency, pendency).Should().BeTrue();
    }

    [Fact]
    public void NotLetTheMasterDeleteSomebodyElsesPendency()
    {
        var pendency = new PostPendency { CreatedBy = new GeneralUser { UserId = PlayerId } };
        var user = Create.User(MasterId).WithRole(UserRole.Admin).Please();

        // Deleting a pendency is reserved to whoever raised it, with no override
        // for the game leads or for site moderation.
        resolver.IsAllowed(user, RoomIntention.DeletePostPendency, pendency).Should().BeFalse();
    }

    [Theory]
    [InlineData(RoomIntention.CreatePost)]
    [InlineData(RoomIntention.CreatePostPendency)]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void RefuseEveryIntentionOtherThanDeleteOnThePendencyOverload(RoomIntention intention)
    {
        var pendency = new PostPendency { CreatedBy = new GeneralUser { UserId = PlayerId } };
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, pendency).Should().BeFalse();
    }

    #endregion

    #region Reader rows without a user

    [Fact]
    public void DenyOnAReaderRowWithNoUserFromEitherOverload()
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithAccessType(RoomAccessType.Private)
            .WithReaderAccessMissingItsUser()
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        // Both overloads scan the same reader rows through one predicate, so they
        // answer the same way. CreatePost used to dereference a.User outright while
        // ViewMessages went through a.User?., which made one access row throw on one
        // path and deny on the other. A row whose user the projection did not fill
        // names nobody, and grants nobody anything.
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeFalse();
        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeFalse();
    }

    #endregion
}
