using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Forum.Features.Boards;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Boards;

public class BoardServiceShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IAccessPolicyConverter> _accessPolicyConverter;
    private readonly Mock<IBoardRepository> _boardRepository;
    private readonly Mock<IBoardModeratorRepository> _moderatorRepository;
    private readonly Mock<IUserReadRepository> _userRepository;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<ICache> _cache;
    private readonly BoardService _service;

    public BoardServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.Guest());

        _accessPolicyConverter = Mock<IAccessPolicyConverter>();
        _accessPolicyConverter.Setup(c => c.Convert(It.IsAny<UserRole>()))
            .Returns(BoardAccessPolicy.Guest);

        _boardRepository = Mock<IBoardRepository>();
        _moderatorRepository = Mock<IBoardModeratorRepository>();
        _userRepository = Mock<IUserReadRepository>();
        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _cache = Mock<ICache>();

        _service = new BoardService(
            _identityProvider.Object,
            _accessPolicyConverter.Object,
            _boardRepository.Object,
            _moderatorRepository.Object,
            _userRepository.Object,
            _unreadCountersRepository.Object,
            _cache.Object);
    }

    [Fact]
    public async Task ReturnBoardsListForGuestUser()
    {
        var boards = new[]
        {
            new Board { Id = Guid.NewGuid(), Title = "General", Description = "General discussion" },
            new Board { Id = Guid.NewGuid(), Title = "News", Description = "News and updates" }
        };
        _boardRepository.Setup(r => r.SelectBoards(It.IsAny<BoardAccessPolicy?>()))
            .ReturnsAsync(boards);

        var result = await _service.GetBoardsList();

        result.Should().BeEquivalentTo(boards);
    }

    [Fact]
    public async Task FillUnreadCountersForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var identity = Identities.User(userId, UserRole.RegularUser);
        _identityProvider.Setup(p => p.Current).Returns(identity);

        var boards = new[]
        {
            new Board { Id = Guid.NewGuid(), Title = "General" }
        };
        _boardRepository.Setup(r => r.SelectBoards(It.IsAny<BoardAccessPolicy?>()))
            .ReturnsAsync(boards);

        var emptyCounters = new Dictionary<Guid, int> { { boards[0].Id, 0 } };
        _unreadCountersRepository
            .Setup(r => r.SelectByParentsAsync(userId, UnreadEntryType.Message, It.IsAny<Guid[]>()))
            .ReturnsAsync(emptyCounters);
        _unreadCountersRepository
            .Setup(r => r.SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, It.IsAny<Guid[]>()))
            .ReturnsAsync(emptyCounters);

        await _service.GetBoardsList();

        _unreadCountersRepository.Verify(
            r => r.SelectByParentsAsync(userId, UnreadEntryType.Message, It.IsAny<Guid[]>()),
            Times.Once);
        _unreadCountersRepository.Verify(
            r => r.SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, It.IsAny<Guid[]>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSingleBoardWithUnreadCounters()
    {
        var userId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var identity = Identities.User(userId, UserRole.RegularUser);
        _identityProvider.Setup(p => p.Current).Returns(identity);

        var board = new Board { Id = boardId, Title = "General" };
        _boardRepository.Setup(r => r.SelectBoards(It.IsAny<BoardAccessPolicy?>()))
            .ReturnsAsync(new[] { board });

        var unreadCounters = new Dictionary<Guid, int> { { boardId, 5 } };
        _unreadCountersRepository
            .Setup(r => r.SelectByParentsAsync(userId, UnreadEntryType.Message, boardId))
            .ReturnsAsync(unreadCounters);
        _unreadCountersRepository
            .Setup(r => r.SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, boardId))
            .ReturnsAsync(unreadCounters);

        var result = await _service.GetSingleBoard("General");

        result.UnreadTopicsCount.Should().Be(5);
        result.UnreadCommentsCount.Should().Be(5);
    }

    [Fact]
    public async Task ThrowWhenBoardNotFound()
    {
        _boardRepository.Setup(r => r.SelectBoards(It.IsAny<BoardAccessPolicy?>()))
            .ReturnsAsync(Array.Empty<Board>());

        var act = async () => await _service.GetBoard("NonExistent");

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Gone);
        exception.Which.Message.Should().Contain("Раздел NonExistent не найден");
    }

    [Fact]
    public async Task GetModeratorsFromCache()
    {
        var boardId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        _boardRepository.Setup(r => r.SelectBoards(It.IsAny<BoardAccessPolicy?>()))
            .ReturnsAsync(new[] { board });

        var moderators = new[]
        {
            new GeneralUser { UserId = Guid.NewGuid(), Username = "Moderator1" }
        };
        _cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<Task<IEnumerable<GeneralUser>>>>(),
                It.IsAny<TimeSpan>()))
            .ReturnsAsync(moderators);

        var result = await _service.GetModerators("General");

        result.Should().BeEquivalentTo(moderators);
        _cache.Verify(c => c.GetOrCreateAsync(
            $"board_moderators_{boardId}",
            It.IsAny<Func<Task<IEnumerable<GeneralUser>>>>(),
            CachePolicy.LongLived), Times.Once);
    }
}
