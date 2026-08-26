using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
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
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Boards;

public class BoardServiceShould : UnitTestBase
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IAccessPolicyConverter _accessPolicyConverter;
    private readonly IBoardRepository _boardRepository;
    private readonly IBoardModeratorRepository _moderatorRepository;
    private readonly IUserReadRepository _userRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly ICache _cache;
    private readonly BoardService _service;

    public BoardServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.Guest());

        _accessPolicyConverter = Mock<IAccessPolicyConverter>();
        _accessPolicyConverter.Convert(Arg.Any<UserRole>()).Returns(BoardAccessPolicy.Guest);

        _boardRepository = Mock<IBoardRepository>();
        _moderatorRepository = Mock<IBoardModeratorRepository>();
        _userRepository = Mock<IUserReadRepository>();
        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _cache = Mock<ICache>();

        _service = new BoardService(
            _identityProvider,
            _accessPolicyConverter,
            _boardRepository,
            _moderatorRepository,
            _userRepository,
            _unreadCountersRepository,
            _cache);
    }

    [Fact]
    public async Task ReturnBoardsListForGuestUser()
    {
        var boards = new[]
        {
            new Board { Id = Guid.NewGuid(), Title = "General", Description = "General discussion" },
            new Board { Id = Guid.NewGuid(), Title = "News", Description = "News and updates" }
        };
        _boardRepository.SelectBoards(Arg.Any<BoardAccessPolicy?>()).Returns(boards);

        var result = await _service.GetBoardsList();

        result.Should().BeEquivalentTo(boards);
    }

    [Fact]
    public async Task FillUnreadCountersForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var identity = Identities.User(userId, UserRole.RegularUser);
        _identityProvider.Current.Returns(identity);

        var boards = new[]
        {
            new Board { Id = Guid.NewGuid(), Title = "General" }
        };
        _boardRepository.SelectBoards(Arg.Any<BoardAccessPolicy?>()).Returns(boards);

        var emptyCounters = new Dictionary<Guid, int> { { boards[0].Id, 0 } };
        _unreadCountersRepository
            .SelectByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>()).Returns(emptyCounters);
        _unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>()).Returns(emptyCounters);

        await _service.GetBoardsList();

        await _unreadCountersRepository.Received(1).SelectByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>());
        await _unreadCountersRepository.Received(1).SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>());
    }

    [Fact]
    public async Task GetSingleBoardWithUnreadCounters()
    {
        var userId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var identity = Identities.User(userId, UserRole.RegularUser);
        _identityProvider.Current.Returns(identity);

        var board = new Board { Id = boardId, Title = "General" };
        _boardRepository.SelectBoards(Arg.Any<BoardAccessPolicy?>()).Returns(new[] { board });

        var unreadCounters = new Dictionary<Guid, int> { { boardId, 5 } };
        _unreadCountersRepository
            .SelectByParentsAsync(userId, UnreadEntryType.Message, boardId).Returns(unreadCounters);
        _unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, boardId).Returns(unreadCounters);

        var result = await _service.GetSingleBoard("General");

        result.UnreadTopicsCount.Should().Be(5);
        result.UnreadCommentsCount.Should().Be(5);
    }

    /// <summary>
    /// Both counter reads of a single board go to the one DbContext of the
    /// request scope, which serves one operation at a time. Substitutes that
    /// answer instantly cannot show the difference, so this one holds the first
    /// read open and refuses a second read that arrives while it is in flight -
    /// the same refusal the context itself raises.
    /// </summary>
    [Fact]
    public async Task ReadTheCountersOfOneBoardOneAfterTheOther()
    {
        var userId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        _identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));

        var board = new Board { Id = boardId, Title = "General" };
        _boardRepository.SelectBoards(Arg.Any<BoardAccessPolicy?>()).Returns(new[] { board });

        var busy = 0;
        Task<IDictionary<Guid, int>> OneAtATime(int counter)
        {
            if (Interlocked.Exchange(ref busy, 1) == 1)
            {
                throw new InvalidOperationException(
                    "A second operation was started on this context instance before a previous operation completed.");
            }

            return Task.Run(async () =>
            {
                await Task.Delay(20);
                Interlocked.Exchange(ref busy, 0);
                return (IDictionary<Guid, int>)new Dictionary<Guid, int> { { boardId, counter } };
            });
        }

        _unreadCountersRepository
            .SelectByParentsAsync(userId, UnreadEntryType.Message, boardId)
            .Returns(_ => OneAtATime(3));
        _unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, boardId)
            .Returns(_ => OneAtATime(7));

        var result = await _service.GetSingleBoard("General");

        result.UnreadTopicsCount.Should().Be(3);
        result.UnreadCommentsCount.Should().Be(7);
    }

    [Fact]
    public async Task ThrowWhenBoardNotFound()
    {
        _boardRepository.SelectBoards(Arg.Any<BoardAccessPolicy?>()).Returns(Array.Empty<Board>());

        var act = async () => await _service.GetBoard("NonExistent");

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Which.Message.Should().Contain("Раздел NonExistent не найден");
    }

    [Fact]
    public async Task GetModeratorsFromCache()
    {
        var boardId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        _boardRepository.SelectBoards(Arg.Any<BoardAccessPolicy?>()).Returns(new[] { board });

        var moderators = new[]
        {
            new GeneralUser { UserId = Guid.NewGuid(), Username = "Moderator1" }
        };
        _cache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<IEnumerable<GeneralUser>>>>(),
                Arg.Any<TimeSpan>()).Returns(moderators);

        var result = await _service.GetModerators("General");

        result.Should().BeEquivalentTo(moderators);
        await _cache.Received(1).GetOrCreateAsync(
            $"board_moderators_{boardId}",
            Arg.Any<Func<Task<IEnumerable<GeneralUser>>>>(),
            CachePolicy.LongLived);
    }
}
