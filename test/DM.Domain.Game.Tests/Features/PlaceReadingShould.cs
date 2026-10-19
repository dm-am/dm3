using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Posts;
using DM.Testing;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features;

/// <summary>
/// A game or a post is read for the warning log by its own read, on the token the log was asked with.
/// </summary>
public class PlaceReadingShould : UnitTestBase
{
    [Fact]
    public async Task ReadAGameOnTheTokenItIsHanded()
    {
        var games = Mock<IGameService>();
        var gameId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();

        await new GamePlaceReading(games).ReadableAsync([gameId], cancellation.Token);

        await games.Received(1).GetAsync(gameId, cancellation.Token);
    }

    [Fact]
    public async Task ReadAPostOnTheTokenItIsHanded()
    {
        var posts = Mock<IPostService>();
        var postId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();

        await new PostPlaceReading(posts).ReadableAsync([postId], cancellation.Token);

        await posts.Received(1).GetAsync(postId, cancellation.Token);
    }
}
