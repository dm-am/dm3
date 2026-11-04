using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

/// <summary>
/// Gates who may post in a room and as whom. The expensive mistake here is not a
/// locked door but an open one: posting under another player's character puts
/// words in their mouth.
/// </summary>
public class RoomPostIntentionShould : RoomIntentionTestBase
{
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
    /// is what says it no longer plays.
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
    /// only the characters with such an access (PostComposer.vue mirrors this arm),
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
        // the character may write there. ReadOnly is a seat in the audience.
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)characterId)).Should().BeFalse();
    }

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
}
