using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Posts;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Posts;

// A roll sent without "Показывать всем результаты бросков", and every roll of a game or a room that
// does not show the results to everybody, is read by the master, the assistants and the author of
// the post, who is the one that rolled.
public class PostDiceRollVisibilityShould : PostServiceTestBase
{
    private static readonly Guid MasterId = Guid.Parse("3c1e0d22-0000-4000-8000-000000000001");
    private static readonly Guid AssistantId = Guid.Parse("3c1e0d22-0000-4000-8000-000000000002");
    private static readonly Guid AuthorId = Guid.Parse("3c1e0d22-0000-4000-8000-000000000003");
    private static readonly Guid StrangerId = Guid.Parse("3c1e0d22-0000-4000-8000-000000000004");

    private readonly Guid roomId = Guid.NewGuid();

    public static TheoryData<Guid> Leads => new() { MasterId, AssistantId, AuthorId };

    [Fact]
    public async Task LeaveAHiddenRollOutForAReaderWhoIsNoLeadAndDidNotRoll()
    {
        var post = PostReadBy(StrangerId);
        RollsOf(post, Roll(post, isHidden: true));

        var read = await service.GetAsync(post.Id);

        read.DiceRolls.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Leads))]
    public async Task ShowAHiddenRollToTheMasterTheAssistantsAndWhoeverRolled(Guid readerId)
    {
        var post = PostReadBy(readerId);
        var roll = Roll(post, isHidden: true);
        RollsOf(post, roll);

        var read = await service.GetAsync(post.Id);

        read.DiceRolls.Should().Equal(roll);
    }

    [Fact]
    public async Task ShowAPublicRollToEveryReader()
    {
        var post = PostReadBy(StrangerId);
        var roll = Roll(post);
        RollsOf(post, roll);

        var read = await service.GetAsync(post.Id);

        read.DiceRolls.Should().Equal(roll);
    }

    [Fact]
    public async Task LeaveEveryRollOutForAStrangerWhenTheGameHidesTheResults()
    {
        var post = PostReadBy(StrangerId);
        RollsOf(post, Roll(post, gameHidesResults: true));

        var read = await service.GetAsync(post.Id);

        read.DiceRolls.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveEveryRollOutForAStrangerWhenTheRoomDoesNotShowTheResults()
    {
        var post = PostReadBy(StrangerId);
        RollsOf(post, Roll(post, roomShowsResults: false));

        var read = await service.GetAsync(post.Id);

        read.DiceRolls.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Leads))]
    public async Task ShowTheRollsToTheLeadsAndWhoeverRolledWhenNeitherTheGameNorTheRoomShowsThem(Guid readerId)
    {
        var post = PostReadBy(readerId);
        var roll = Roll(post, gameHidesResults: true, roomShowsResults: false);
        RollsOf(post, roll);

        var read = await service.GetAsync(post.Id);

        read.DiceRolls.Should().Equal(roll);
    }

    // The post of a removed account is signed by nobody, and nobody is the id a guest carries: the
    // guest is not the one who rolled.
    [Fact]
    public async Task LeaveAHiddenRollOutForAGuestOnAPostSignedByNobody()
    {
        identityProvider.Current.Returns(Identities.Guest());
        var post = NewPost();
        post.AuthorUserId = AnonymousIdentity.UserId;
        repository.Get(post.Id, Arg.Any<Guid>()).Returns(post);
        RollsOf(post, Roll(post, isHidden: true));

        var read = await service.GetAsync(post.Id);

        read.DiceRolls.Should().BeEmpty();
    }

    [Fact]
    public async Task LeaveTheHiddenRollsOutOfTheRoomPageAndKeepThePublicOnes()
    {
        identityProvider.Current.Returns(Identities.User(StrangerId));
        var post = NewPost();
        var hidden = Roll(post, isHidden: true);
        var shown = Roll(post);
        roomService.GetAsync(roomId).Returns(new Room { Id = roomId });
        repository.Count(roomId, Arg.Any<RoomPostsQuery>(), StrangerId).Returns(1);
        repository.Get(roomId, Arg.Any<RoomPostsQuery>(), Arg.Any<PagingData>(), StrangerId)
            .Returns(new[] { post });
        diceRollRepository.GetByPostIdsAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new Dictionary<Guid, IEnumerable<DiceRoll>> { [post.Id] = new[] { hidden, shown } });

        var (posts, _) = await service.GetAllAsync(roomId, new RoomPostsQuery());

        posts.Single().DiceRolls.Should().Equal(shown);
    }

    private Post NewPost() => new()
    {
        Id = Guid.NewGuid(),
        RoomId = roomId,
        AuthorUserId = AuthorId,
        GameMasterUserId = MasterId,
        GameAssistantUserIds = [AssistantId]
    };

    private Post PostReadBy(Guid readerId)
    {
        identityProvider.Current.Returns(Identities.User(readerId));
        var post = NewPost();
        repository.Get(post.Id, readerId).Returns(post);
        return post;
    }

    private void RollsOf(Post post, params DiceRoll[] rolls) =>
        diceRollRepository.GetByPostIdAsync(post.Id).Returns(rolls);

    private static DiceRoll Roll(
        Post post, bool isHidden = false, bool gameHidesResults = false, bool roomShowsResults = true) => new()
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            IsHidden = isHidden,
            GameHideDiceResult = gameHidesResults,
            RoomViewDiceResults = roomShowsResults,
            DiceCount = 1,
            EdgesCount = 20,
            Results = [new DiceRollResult { Value = 17 }]
        };
}
