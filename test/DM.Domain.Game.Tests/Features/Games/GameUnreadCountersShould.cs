using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GameUnreadCountersShould : UnitTestBase
{
    private readonly IGameRepository repository = Mock<IGameRepository>();
    private readonly IUnreadCountersRepository unreadCountersRepository = Mock<IUnreadCountersRepository>();
    private readonly GameUnreadCounters counters;

    public GameUnreadCountersShould()
    {
        counters = new GameUnreadCounters(repository, unreadCountersRepository);
    }

    [Fact]
    public async Task ShowAGuestTheTotalsAndZeroForAGameWithout()
    {
        var counted = new GameDto { Id = Guid.NewGuid() };
        var silent = new GameDto { Id = Guid.NewGuid() };
        repository.GetTotalPostCounts(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [counted.Id] = 7 });
        repository.GetTotalCommentCounts(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [counted.Id] = 3 });

        await counters.FillTotals([counted, silent]);

        (counted.UnreadPostsCount, counted.UnreadCommentsCount).Should().Be((7, 3));
        (silent.UnreadPostsCount, silent.UnreadCommentsCount).Should().Be((0, 0));
    }

    [Fact]
    public async Task SumTheUnreadPostsOfTheRoomsTheReaderMayOpen()
    {
        var userId = Guid.NewGuid();
        var game = new GameDto { Id = Guid.NewGuid() };
        var (open, other) = (Guid.NewGuid(), Guid.NewGuid());
        repository.GetAvailableRoomIds(Arg.Any<IEnumerable<Guid>>(), userId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, IEnumerable<Guid>> { [game.Id] = [open, other] });
        unreadCountersRepository.SelectByEntitiesAsync(userId, Arg.Any<UnreadEntryType>(), Arg.Any<Guid[]>())
            .Returns(ci => ci.ArgAt<Guid[]>(2).ToDictionary(id => id, id =>
                id == open ? 4
                : id == other ? 5
                : ci.ArgAt<UnreadEntryType>(1) == UnreadEntryType.Character ? 2 : 1) as IDictionary<Guid, int>);

        await counters.FillUnread([game], userId);

        game.UnreadPostsCount.Should().Be(9);
        (game.UnreadCommentsCount, game.UnreadCharactersCount).Should().Be((1, 2));
    }
}
