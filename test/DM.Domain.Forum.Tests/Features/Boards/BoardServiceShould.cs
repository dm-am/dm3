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
    private readonly IIdentityProvider identityProvider;
    private readonly IBoardRepository boardRepository;
    private readonly IBoardModeratorRepository moderatorRepository;
    private readonly IUserReadRepository userRepository;
    private readonly IUnreadCountersRepository unreadCountersRepository;
    private readonly ICache cache;
    private readonly BoardService service;

    public BoardServiceShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.Guest());

        boardRepository = Mock<IBoardRepository>();
        moderatorRepository = Mock<IBoardModeratorRepository>();
        userRepository = Mock<IUserReadRepository>();
        unreadCountersRepository = Mock<IUnreadCountersRepository>();
        cache = Mock<ICache>();

        service = new BoardService(
            identityProvider,
            boardRepository,
            moderatorRepository,
            userRepository,
            unreadCountersRepository,
            cache);
    }

    [Fact]
    public async Task FillUnreadCountersForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var identity = Identities.User(userId, UserRole.RegularUser);
        identityProvider.Current.Returns(identity);

        var boards = new[]
        {
            new Board { Id = Guid.NewGuid(), Title = "General" }
        };
        boardRepository.SelectBoards().Returns(boards);

        var emptyCounters = new Dictionary<Guid, int> { { boards[0].Id, 0 } };
        unreadCountersRepository
            .SelectByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>()).Returns(emptyCounters);
        unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>()).Returns(emptyCounters);

        await service.GetBoardsList();

        await unreadCountersRepository.Received(1).SelectByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>());
        await unreadCountersRepository.Received(1).SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>());
    }

    [Fact]
    public async Task KeepEveryFieldOfTheBoardForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));

        var moderator = new GeneralUser { UserId = Guid.NewGuid(), Username = "Moderator1" };
        var board = new Board
        {
            Id = Guid.NewGuid(),
            Title = "General",
            Alias = "general",
            Description = "Talk",
            CreateTopicPolicy = BoardAccessPolicy.RegularUser,
            ModeratorIds = [moderator.UserId],
            Moderators = [moderator],
            TopicsCount = 4,
            CommentsCount = 9,
            LastComment = new BoardLastComment { Id = Guid.NewGuid(), TopicTitle = "Hello" },
            LastTopic = new BoardLastTopic { Id = Guid.NewGuid(), TopicNumber = 4, Title = "Hello" }
        };
        boardRepository.SelectBoards().Returns(new[] { board });
        unreadCountersRepository
            .SelectByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { { board.Id, 2 } });
        unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, Arg.Any<Guid[]>())
            .Returns(new Dictionary<Guid, int> { { board.Id, 3 } });

        var listed = (await service.GetBoardsList()).Should().ContainSingle().Subject;

        listed.Should().BeEquivalentTo(board, options => options
            .Excluding(b => b.UnreadTopicsCount)
            .Excluding(b => b.UnreadCommentsCount));
        listed.UnreadTopicsCount.Should().Be(2);
        listed.UnreadCommentsCount.Should().Be(3);
    }

    [Fact]
    public async Task GetSingleBoardWithUnreadCounters()
    {
        var userId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var identity = Identities.User(userId, UserRole.RegularUser);
        identityProvider.Current.Returns(identity);

        var board = new Board { Id = boardId, Title = "General" };
        boardRepository.SelectBoards().Returns(new[] { board });

        var unreadCounters = new Dictionary<Guid, int> { { boardId, 5 } };
        unreadCountersRepository
            .SelectByParentsAsync(userId, UnreadEntryType.Message, boardId).Returns(unreadCounters);
        unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, boardId).Returns(unreadCounters);

        var result = await service.GetBoardWithCounters("General");

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
        identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));

        var board = new Board { Id = boardId, Title = "General" };
        boardRepository.SelectBoards().Returns(new[] { board });

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

        unreadCountersRepository
            .SelectByParentsAsync(userId, UnreadEntryType.Message, boardId)
            .Returns(_ => OneAtATime(3));
        unreadCountersRepository
            .SelectTotalUnreadByParentsAsync(userId, UnreadEntryType.Message, boardId)
            .Returns(_ => OneAtATime(7));

        var result = await service.GetBoardWithCounters("General");

        result.UnreadTopicsCount.Should().Be(3);
        result.UnreadCommentsCount.Should().Be(7);
    }

    [Fact]
    public async Task ThrowWhenBoardNotFound()
    {
        boardRepository.SelectBoards().Returns(Array.Empty<Board>());

        var act = async () => await service.GetBoard("NonExistent");

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Which.Message.Should().Contain("Раздел NonExistent не найден");
    }

    [Fact]
    public async Task GetModeratorsFromCache()
    {
        var boardId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        boardRepository.SelectBoards().Returns(new[] { board });

        var moderators = new[]
        {
            new GeneralUser { UserId = Guid.NewGuid(), Username = "Moderator1" }
        };
        cache.GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<Func<Task<IEnumerable<GeneralUser>>>>(),
                Arg.Any<TimeSpan>()).Returns(moderators);

        var result = await service.GetModerators("General");

        result.Should().BeEquivalentTo(moderators);
        await cache.Received(1).GetOrCreateAsync(
            $"board_moderators_{boardId}",
            Arg.Any<Func<Task<IEnumerable<GeneralUser>>>>(),
            CachePolicy.LongLived);
    }
}
