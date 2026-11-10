using System;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;
using GameDto = DM.Domain.Game.Features.Games.Game;

namespace DM.Domain.Game.Tests.Authorization;

// Reading a chat room hands over its private history, and writing in it speaks in front of
// everybody it was opened to.
public class ChatRoomIntentionShould : RoomIntentionTestBase
{
    // The rooms below are closed unless a case says otherwise: in an open room
    // anybody reads, so only a closed one shows what a row admits to reading.

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void LetAReaderIntoAChatRoom(RoomIntention intention)
    {
        var room = ChatRoom(RoomAccessType.Private)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, room).Should().BeTrue();
    }

    [Fact]
    public void LetAReaderGrantedReadOnlyReadAChatRoomAndNotWriteInIt()
    {
        var room = ChatRoom(RoomAccessType.Private)
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
        var room = ChatRoom(RoomAccessType.Private, GameLedBy(MasterId, AssistantId)).Please();
        var user = Create.User(AssistantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, room).Should().BeTrue();
    }

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void KeepAStrangerOutOfAClosedChatRoom(RoomIntention intention)
    {
        var room = ChatRoom(RoomAccessType.Private)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, room).Should().BeFalse();
    }

    [Theory]
    [InlineData(RoomAccessType.Open)]
    [InlineData(RoomAccessType.Private)]
    public void LetACharacterTheChatRoomWasOpenedToReadAndWriteInIt(RoomAccessType access)
    {
        var room = ChatRoom(access)
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
        var room = ChatRoom(access)
            .WithCharacterAccess(Guid.NewGuid(), PlayerId, policy: RoomAccessPolicy.ReadOnly)
            .Please();
        var user = Create.User(PlayerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeFalse();
    }

    // A character out of play writes nothing in a chat room, and its rows still open the room to
    // read, as they open its page.
    [Theory]
    [InlineData(CharacterStatus.Retired)]
    [InlineData(CharacterStatus.UnderReview)]
    [InlineData(CharacterStatus.Declined)]
    public void KeepACharacterOutOfPlayFromWritingInAChatRoom(CharacterStatus status)
    {
        var room = ChatRoom(RoomAccessType.Private)
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
        var room = ChatRoom(RoomAccessType.Private)
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
        var room = ChatRoom(RoomAccessType.Private)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        // Room access is a game role question, not a site role one: no site rank
        // opens a private room.
        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeFalse();
    }

    // An open chat room is read by everybody who sees the game, which RoomAvailable has already
    // asked.
    [Fact]
    public void LetAnybodyWhoSeesTheGameReadAnOpenChatRoom()
    {
        var room = ChatRoom(RoomAccessType.Open)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
    }

    // Taking part in the game is not the grant, the row is.
    [Fact]
    public void NotLetAPlayerTheOpenChatRoomDidNotAddWriteInIt()
    {
        var game = new GameBuilder().WithMaster(MasterId).WithPlayers(PlayerId, StrangerId).Please();
        var room = ChatRoom(RoomAccessType.Open, game)
            .WithCharacterAccess(Guid.NewGuid(), PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeTrue();
        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeFalse();
    }

    // Nor does an open room give a voice to a site rank: openness is about reading for an admin as
    // much as for anybody else.
    [Fact]
    public void NotLetAnAdminWriteInAnOpenChatRoomTheyHaveNoPartIn()
    {
        var room = ChatRoom(RoomAccessType.Open).Please();
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        resolver.IsAllowed(user, RoomIntention.SendMessage, room).Should().BeFalse();
    }

    private static RoomBuilder ChatRoom(RoomAccessType access, GameDto? game = null) =>
        new RoomBuilder().WithGame(game ?? GameLedBy(MasterId)).WithType(RoomType.Chat).WithAccessType(access);
}
