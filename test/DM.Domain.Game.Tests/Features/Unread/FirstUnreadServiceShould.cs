using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Rooms;
using DM.Domain.Game.Features.Unread;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Unread;

public class FirstUnreadServiceShould : UnitTestBase
{
    private readonly IFirstUnreadRepository firstUnreadRepository;
    private readonly IGameService gameService;
    private readonly IUnreadCountersRepository unreadCountersRepository;
    private readonly IIdentityProvider identityProvider;
    private readonly IIntentionManager intentionManager;
    private readonly FirstUnreadService service;

    public FirstUnreadServiceShould()
    {
        firstUnreadRepository = Mock<IFirstUnreadRepository>();
        gameService = Mock<IGameService>();
        unreadCountersRepository = Mock<IUnreadCountersRepository>();
        identityProvider = Mock<IIdentityProvider>();
        intentionManager = Mock<IIntentionManager>();

        service = new FirstUnreadService(
            firstUnreadRepository,
            gameService,
            Mock<IRoomService>(),
            unreadCountersRepository,
            identityProvider,
            intentionManager);
    }

    [Fact]
    public async Task AuthorizeGetFirstUnreadPostAction()
    {
        var gameId = Guid.NewGuid();
        var game = GameBuilder.Bare(gameId);

        gameService.GetAsync(gameId).Returns(game);
        identityProvider.Current.Returns(Identities.Guest());
        firstUnreadRepository.GetAccessibleRoomIds(gameId, Arg.Any<Guid>()).Returns(new List<Guid>());

        await service.GetFirstUnreadPost(gameId);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.Read, game);
    }

    [Fact]
    public async Task ReturnNoUnreadWhenNoAccessibleRooms()
    {
        var gameId = Guid.NewGuid();

        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), UserRole.RegularUser));
        firstUnreadRepository.GetAccessibleRoomIds(gameId, Arg.Any<Guid>()).Returns(new List<Guid>());

        var result = await service.GetFirstUnreadPost(gameId);

        result.HasUnread.Should().BeFalse();
    }

    [Theory]
    [InlineData(21, 3)]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(0, 1)]
    public async Task LocateThePageOfTheFirstUnreadPostAtThePageSize(int postNumber, int expectedPage)
    {
        var roomId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.Guest());
        firstUnreadRepository.LocateInRoom(roomId, Arg.Any<DateTime>())
            .Returns(new FirstUnreadRoomPostResult { PostNumber = postNumber });

        var result = await service.GetFirstUnreadPostInRoom(roomId, 10);

        result.PageSize.Should().Be(10);
        result.Page.Should().Be(expectedPage);
    }

    [Fact]
    public async Task ReturnFirstPostForAnonymousUser()
    {
        var gameId = Guid.NewGuid();
        var roomIds = new List<Guid> { Guid.NewGuid() };
        var expectedResult = new FirstUnreadPostResult { HasUnread = true, PostId = Guid.NewGuid() };

        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        identityProvider.Current.Returns(Identities.Guest());
        firstUnreadRepository.GetAccessibleRoomIds(gameId, Arg.Any<Guid>()).Returns(roomIds);
        firstUnreadRepository.GetFirstPostInRooms(roomIds).Returns(expectedResult);

        var result = await service.GetFirstUnreadPost(gameId);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task FindFirstUnreadPostForAuthenticatedUser()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roomIds = new List<Guid> { Guid.NewGuid() };
        var lastReadTimes = new Dictionary<Guid, DateTime> { { roomIds[0], DateTime.UtcNow.AddDays(-1) } };
        var expectedResult = new FirstUnreadPostResult { HasUnread = true, PostId = Guid.NewGuid() };

        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));
        firstUnreadRepository.GetAccessibleRoomIds(gameId, userId).Returns(roomIds);
        unreadCountersRepository.GetLastReadTimesAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(lastReadTimes);
        firstUnreadRepository.FindFirstUnreadPost(roomIds, lastReadTimes).Returns(expectedResult);

        var result = await service.GetFirstUnreadPost(gameId);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ReturnLastPostWhenNoUnreadPosts()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roomIds = new List<Guid> { Guid.NewGuid() };
        var lastReadTimes = new Dictionary<Guid, DateTime> { { roomIds[0], DateTime.UtcNow.AddDays(-1) } };
        var expectedResult = new FirstUnreadPostResult { HasUnread = false, PostId = Guid.NewGuid() };

        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));
        firstUnreadRepository.GetAccessibleRoomIds(gameId, userId).Returns(roomIds);
        unreadCountersRepository.GetLastReadTimesAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(lastReadTimes);
        firstUnreadRepository.FindFirstUnreadPost(roomIds, lastReadTimes).Returns((FirstUnreadPostResult?)null);
        firstUnreadRepository.GetLastPostInRooms(roomIds).Returns(expectedResult);

        var result = await service.GetFirstUnreadPost(gameId);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task FindTheFirstUnreadCommentOfWhateverGameTheReaderMayOpen()
    {
        var gameId = Guid.NewGuid();
        var game = GameBuilder.Bare(gameId);

        gameService.GetAsync(gameId).Returns(game);
        identityProvider.Current.Returns(Identities.Guest());
        firstUnreadRepository.GetFirstComment(gameId).Returns(new FirstUnreadCommentResult { HasUnread = false });

        await service.GetFirstUnreadComment(gameId);

        // The discussion is read by whoever may read the game: nothing is asked after.
        await gameService.Received(1).GetAsync(gameId);
        intentionManager.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ReturnFirstCommentForAnonymousUser()
    {
        var gameId = Guid.NewGuid();
        var expectedResult = new FirstUnreadCommentResult { HasUnread = true, CommentId = Guid.NewGuid() };

        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        identityProvider.Current.Returns(Identities.Guest());
        firstUnreadRepository.GetFirstComment(gameId).Returns(expectedResult);

        var result = await service.GetFirstUnreadComment(gameId);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task FindFirstUnreadCommentForAuthenticatedUser()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lastRead = DateTime.UtcNow.AddDays(-1);
        var expectedResult = new FirstUnreadCommentResult { HasUnread = true, CommentId = Guid.NewGuid() };

        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));
        unreadCountersRepository.GetLastReadTimeAsync(userId, gameId, UnreadEntryType.Message).Returns(lastRead);
        firstUnreadRepository.FindFirstUnreadComment(gameId, lastRead).Returns(expectedResult);

        var result = await service.GetFirstUnreadComment(gameId);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ReturnLastCommentWhenNoUnreadComments()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lastRead = DateTime.UtcNow.AddDays(-1);
        var expectedResult = new FirstUnreadCommentResult { HasUnread = false, CommentId = Guid.NewGuid() };

        gameService.GetAsync(gameId).Returns(GameBuilder.Bare(gameId));
        identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));
        unreadCountersRepository.GetLastReadTimeAsync(userId, gameId, UnreadEntryType.Message).Returns(lastRead);
        firstUnreadRepository.FindFirstUnreadComment(gameId, lastRead).Returns((FirstUnreadCommentResult?)null);
        firstUnreadRepository.GetLastComment(gameId).Returns(expectedResult);

        var result = await service.GetFirstUnreadComment(gameId);

        result.Should().Be(expectedResult);
    }
}
