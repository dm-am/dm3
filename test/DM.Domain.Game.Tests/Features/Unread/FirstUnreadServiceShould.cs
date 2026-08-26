using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Unread;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Unread;

public class FirstUnreadServiceShould : UnitTestBase
{
    private readonly IFirstUnreadRepository _firstUnreadRepository;
    private readonly IGameService _gameService;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IIntentionManager _intentionManager;
    private readonly FirstUnreadService _service;

    public FirstUnreadServiceShould()
    {
        _firstUnreadRepository = Mock<IFirstUnreadRepository>();
        _gameService = Mock<IGameService>();
        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _intentionManager = Mock<IIntentionManager>();


        _service = new FirstUnreadService(
            _firstUnreadRepository,
            _gameService,
            _unreadCountersRepository,
            _identityProvider,
            _intentionManager);
    }

    [Fact]
    public async Task AuthorizeGetFirstUnreadPostAction()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };

        _gameService.GetAsync(gameId).Returns(game);
        _identityProvider.Current.Returns(Identities.Guest());
        _firstUnreadRepository.GetAccessibleRoomIds(gameId, Arg.Any<Guid>()).Returns(new List<Guid>());

        await _service.GetFirstUnreadPost(gameId);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Read, game);
    }

    [Fact]
    public async Task ReturnNoUnreadWhenNoAccessibleRooms()
    {
        var gameId = Guid.NewGuid();

        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), UserRole.RegularUser));
        _firstUnreadRepository.GetAccessibleRoomIds(gameId, Arg.Any<Guid>()).Returns(new List<Guid>());

        var result = await _service.GetFirstUnreadPost(gameId);

        result.HasUnread.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnFirstPostForAnonymousUser()
    {
        var gameId = Guid.NewGuid();
        var roomIds = new List<Guid> { Guid.NewGuid() };
        var expectedResult = new FirstUnreadPostResult { HasUnread = true, PostId = Guid.NewGuid() };

        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Current.Returns(Identities.Guest());
        _firstUnreadRepository.GetAccessibleRoomIds(gameId, Arg.Any<Guid>()).Returns(roomIds);
        _firstUnreadRepository.GetFirstPostInRooms(roomIds).Returns(expectedResult);

        var result = await _service.GetFirstUnreadPost(gameId);

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

        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));
        _firstUnreadRepository.GetAccessibleRoomIds(gameId, userId).Returns(roomIds);
        _unreadCountersRepository.GetLastReadTimesAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(lastReadTimes);
        _firstUnreadRepository.FindFirstUnreadPost(roomIds, lastReadTimes).Returns(expectedResult);

        var result = await _service.GetFirstUnreadPost(gameId);

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

        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));
        _firstUnreadRepository.GetAccessibleRoomIds(gameId, userId).Returns(roomIds);
        _unreadCountersRepository.GetLastReadTimesAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(lastReadTimes);
        _firstUnreadRepository.FindFirstUnreadPost(roomIds, lastReadTimes).Returns((FirstUnreadPostResult?)null);
        _firstUnreadRepository.GetLastPostInRooms(roomIds).Returns(expectedResult);

        var result = await _service.GetFirstUnreadPost(gameId);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task AuthorizeGetFirstUnreadCommentAction()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };

        _gameService.GetAsync(gameId).Returns(game);
        _identityProvider.Current.Returns(Identities.Guest());
        _firstUnreadRepository.GetFirstComment(gameId).Returns(new FirstUnreadCommentResult { HasUnread = false });

        await _service.GetFirstUnreadComment(gameId);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.ReadComments, game);
    }

    [Fact]
    public async Task ReturnFirstCommentForAnonymousUser()
    {
        var gameId = Guid.NewGuid();
        var expectedResult = new FirstUnreadCommentResult { HasUnread = true, CommentId = Guid.NewGuid() };

        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Current.Returns(Identities.Guest());
        _firstUnreadRepository.GetFirstComment(gameId).Returns(expectedResult);

        var result = await _service.GetFirstUnreadComment(gameId);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task FindFirstUnreadCommentForAuthenticatedUser()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lastRead = DateTime.UtcNow.AddDays(-1);
        var expectedResult = new FirstUnreadCommentResult { HasUnread = true, CommentId = Guid.NewGuid() };

        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));
        _unreadCountersRepository.GetLastReadTimeAsync(userId, gameId, UnreadEntryType.Message).Returns(lastRead);
        _firstUnreadRepository.FindFirstUnreadComment(gameId, lastRead).Returns(expectedResult);

        var result = await _service.GetFirstUnreadComment(gameId);

        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ReturnLastCommentWhenNoUnreadComments()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lastRead = DateTime.UtcNow.AddDays(-1);
        var expectedResult = new FirstUnreadCommentResult { HasUnread = false, CommentId = Guid.NewGuid() };

        _gameService.GetAsync(gameId).Returns(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));
        _unreadCountersRepository.GetLastReadTimeAsync(userId, gameId, UnreadEntryType.Message).Returns(lastRead);
        _firstUnreadRepository.FindFirstUnreadComment(gameId, lastRead).Returns((FirstUnreadCommentResult?)null);
        _firstUnreadRepository.GetLastComment(gameId).Returns(expectedResult);

        var result = await _service.GetFirstUnreadComment(gameId);

        result.Should().Be(expectedResult);
    }
}
