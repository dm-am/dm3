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
        // present in a chat room still needs a reader row of their own.
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

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void LetAReaderIntoAChatRoom(RoomIntention intention)
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
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
            .Please();
        var user = Create.User(AssistantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, room).Should().BeTrue();
    }

    [Theory]
    [InlineData(RoomIntention.ViewMessages)]
    [InlineData(RoomIntention.SendMessage)]
    public void KeepAStrangerOutOfAChatRoom(RoomIntention intention)
    {
        var room = new RoomBuilder()
            .WithGame(GameLedBy(MasterId))
            .WithType(RoomType.Chat)
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

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
            .WithReaderAccess(PlayerId)
            .Please();
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        // Room access is a game role question, not a site role one: no site rank
        // opens a private room.
        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeFalse();
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
