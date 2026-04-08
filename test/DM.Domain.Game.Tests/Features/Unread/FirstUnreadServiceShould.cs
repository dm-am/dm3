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
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Unread;

public class FirstUnreadServiceShould : UnitTestBase
{
    private readonly Mock<IFirstUnreadRepository> _firstUnreadRepository;
    private readonly Mock<IGameService> _gameService;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly FirstUnreadService _service;

    public FirstUnreadServiceShould()
    {
        _firstUnreadRepository = Mock<IFirstUnreadRepository>();
        _gameService = Mock<IGameService>();
        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _identityProvider = Mock<IIdentityProvider>();
        _intentionManager = Mock<IIntentionManager>();

        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));

        _service = new FirstUnreadService(
            _firstUnreadRepository.Object,
            _gameService.Object,
            _unreadCountersRepository.Object,
            _identityProvider.Object,
            _intentionManager.Object);
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

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _firstUnreadRepository.Setup(r => r.GetAccessibleRoomIds(gameId, It.IsAny<Guid>()))
            .ReturnsAsync(new List<Guid>());

        await _service.GetFirstUnreadPost(gameId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Read, game), Times.Once);
    }

    [Fact]
    public async Task ReturnNoUnreadWhenNoAccessibleRooms()
    {
        var gameId = Guid.NewGuid();

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(Guid.NewGuid(), UserRole.RegularUser));
        _firstUnreadRepository.Setup(r => r.GetAccessibleRoomIds(gameId, It.IsAny<Guid>()))
            .ReturnsAsync(new List<Guid>());

        var result = await _service.GetFirstUnreadPost(gameId);

        result.HasUnread.Should().BeFalse();
    }

    [Fact]
    public async Task ReturnFirstPostForAnonymousUser()
    {
        var gameId = Guid.NewGuid();
        var roomIds = new List<Guid> { Guid.NewGuid() };
        var expectedResult = new FirstUnreadPostResult { HasUnread = true, PostId = Guid.NewGuid() };

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _firstUnreadRepository.Setup(r => r.GetAccessibleRoomIds(gameId, It.IsAny<Guid>()))
            .ReturnsAsync(roomIds);
        _firstUnreadRepository.Setup(r => r.GetFirstPostInRooms(roomIds))
            .ReturnsAsync(expectedResult);

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

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(userId, UserRole.RegularUser));
        _firstUnreadRepository.Setup(r => r.GetAccessibleRoomIds(gameId, userId))
            .ReturnsAsync(roomIds);
        _unreadCountersRepository.Setup(r => r.GetLastReadTimesAsync(userId, UnreadEntryType.Message, It.IsAny<Guid[]>()))
            .ReturnsAsync(lastReadTimes);
        _firstUnreadRepository.Setup(r => r.FindFirstUnreadPost(roomIds, lastReadTimes))
            .ReturnsAsync(expectedResult);

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

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(userId, UserRole.RegularUser));
        _firstUnreadRepository.Setup(r => r.GetAccessibleRoomIds(gameId, userId))
            .ReturnsAsync(roomIds);
        _unreadCountersRepository.Setup(r => r.GetLastReadTimesAsync(userId, UnreadEntryType.Message, It.IsAny<Guid[]>()))
            .ReturnsAsync(lastReadTimes);
        _firstUnreadRepository.Setup(r => r.FindFirstUnreadPost(roomIds, lastReadTimes))
            .ReturnsAsync((FirstUnreadPostResult?)null);
        _firstUnreadRepository.Setup(r => r.GetLastPostInRooms(roomIds))
            .ReturnsAsync(expectedResult);

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

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _firstUnreadRepository.Setup(r => r.GetFirstComment(gameId))
            .ReturnsAsync(new FirstUnreadCommentResult { HasUnread = false });

        await _service.GetFirstUnreadComment(gameId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.ReadComments, game), Times.Once);
    }

    [Fact]
    public async Task ReturnFirstCommentForAnonymousUser()
    {
        var gameId = Guid.NewGuid();
        var expectedResult = new FirstUnreadCommentResult { HasUnread = true, CommentId = Guid.NewGuid() };

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Setup(p => p.Current).Returns(Identity.Guest());
        _firstUnreadRepository.Setup(r => r.GetFirstComment(gameId))
            .ReturnsAsync(expectedResult);

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

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(userId, UserRole.RegularUser));
        _unreadCountersRepository.Setup(r => r.GetLastReadTimeAsync(userId, gameId, UnreadEntryType.Message))
            .ReturnsAsync(lastRead);
        _firstUnreadRepository.Setup(r => r.FindFirstUnreadComment(gameId, lastRead))
            .ReturnsAsync(expectedResult);

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

        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        });
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(userId, UserRole.RegularUser));
        _unreadCountersRepository.Setup(r => r.GetLastReadTimeAsync(userId, gameId, UnreadEntryType.Message))
            .ReturnsAsync(lastRead);
        _firstUnreadRepository.Setup(r => r.FindFirstUnreadComment(gameId, lastRead))
            .ReturnsAsync((FirstUnreadCommentResult?)null);
        _firstUnreadRepository.Setup(r => r.GetLastComment(gameId))
            .ReturnsAsync(expectedResult);

        var result = await _service.GetFirstUnreadComment(gameId);

        result.Should().Be(expectedResult);
    }
}
