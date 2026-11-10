using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.PostPendencies;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Game.Tests.Authorization;

public class RoomIntentionResolverShould : RoomIntentionTestBase
{
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
        // answer the same way. A row whose user the projection did not fill names
        // nobody, and grants nobody anything.
        resolver.IsAllowed(user, RoomIntention.CreatePost, (room, (Guid?)null)).Should().BeFalse();
        resolver.IsAllowed(user, RoomIntention.ViewMessages, room).Should().BeFalse();
    }
}
