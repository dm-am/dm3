using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

public class PostDiceShould : PostServiceTestBase
{
    [Fact]
    public async Task RollAndPersistDiceWhenRoomDiceEnabled()
    {
        var roomId = Guid.NewGuid();
        var createPost = new CreatePost
        {
            RoomId = roomId,
            GameText = "Test post",
            DiceRolls = new[] { new CreatePostDiceRoll { EdgesCount = 20, DiceCount = 1 } }
        };
        var room = RoomWith(roomId);
        room.Settings = new RoomSettings { DiceEnabled = true };
        var createdPost = new Post { Id = postId, RoomId = roomId };
        var rolledDice = new List<DiceRoll> { new() { Id = Guid.NewGuid(), PostId = postId, EdgesCount = 20 } };

        CreatePostEntity? written = null;
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Create(Arg.Do<CreatePostEntity>(e => written = e))
            .Returns(createdPost);
        diceRoller
            .Roll(postId, Arg.Any<DateTimeOffset>(), Arg.Any<IEnumerable<CreatePostDiceRoll>>()).Returns(rolledDice);

        var result = await service.CreateAsync(createPost);

        diceRoller.Received(1).Roll(postId, Arg.Any<DateTimeOffset>(),
            Arg.Is<IEnumerable<CreatePostDiceRoll>>(s => s.Count() == 1));
        // The rolls travel inside the entity: the repository writes them in the
        // post's own transaction, so both land or neither does.
        written!.DiceRolls.Should().BeSameAs(rolledDice);
        result.DiceRolls.Should().BeSameAs(rolledDice);
    }

    [Fact]
    public async Task NotPersistDiceWhenRoomDiceDisabled()
    {
        var roomId = Guid.NewGuid();
        var createPost = new CreatePost
        {
            RoomId = roomId,
            GameText = "Test post",
            DiceRolls = new[] { new CreatePostDiceRoll { EdgesCount = 20, DiceCount = 1 } }
        };
        var room = RoomWith(roomId);
        room.Settings = new RoomSettings { DiceEnabled = false };

        CreatePostEntity? written = null;
        roomRepository.GetForUpdate(roomId, Arg.Any<Guid>()).Returns(room);
        repository.Create(Arg.Do<CreatePostEntity>(e => written = e))
            .Returns(new Post { Id = Guid.NewGuid(), RoomId = roomId });

        await service.CreateAsync(createPost);

        diceRoller.DidNotReceive().Roll(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(),
            Arg.Any<IEnumerable<CreatePostDiceRoll>>());
        written!.DiceRolls.Should().BeEmpty("dice are dropped when the room has rolling disabled");
    }
}
