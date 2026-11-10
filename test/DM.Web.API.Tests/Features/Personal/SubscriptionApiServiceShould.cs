using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Personal.Features.Subscriptions;
using DM.Testing;
using DM.Web.API.Features.Personal.Subscriptions;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using DomainGame = DM.Domain.Game.Features.Games.Game;
using DomainSubscription = DM.Domain.Core.Subscriptions.Subscription;

namespace DM.Web.API.Tests.Features.Personal;

// The personal service knows nothing of games, so a hidden game answers the 404 of a missing one.
public class SubscriptionApiServiceShould : UnitTestBase
{
    private readonly ISubscriptionService _subscriptionService = Mock<ISubscriptionService>();
    private readonly IGameService _gameService = Mock<IGameService>();
    private readonly SubscriptionApiService _service;

    public SubscriptionApiServiceShould()
    {
        _subscriptionService.SubscribeAsync(
                Arg.Any<SubscriptionTargetType>(), Arg.Any<Guid>(), Arg.Any<SubscriptionSettings?>(),
                Arg.Any<CancellationToken>())
            .Returns(new DomainSubscription());
        _service = new SubscriptionApiService(_subscriptionService, _gameService, new SubscriptionMapper());
    }

    [Fact]
    public async Task RefuseToSubscribeToAGameHiddenFromTheReaderAsIfThereWereNone()
    {
        var gameId = Guid.NewGuid();
        _gameService.GetAsync(gameId)
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound));

        var act = async () => await _service.SubscribeAsync(SubscriptionTargetType.Game, gameId, null);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == RefusalMessage.GameNotFound);
        await _subscriptionService.DidNotReceive().SubscribeAsync(
            Arg.Any<SubscriptionTargetType>(), Arg.Any<Guid>(), Arg.Any<SubscriptionSettings?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeToAGameTheReaderSeesWithTheSettingsAsked()
    {
        var gameId = Guid.NewGuid();
        _gameService.GetAsync(gameId).Returns(new DomainGame { Id = gameId });
        var settings = SubscriptionSettings.GameReaderDefault;

        await _service.SubscribeAsync(SubscriptionTargetType.Game, gameId, new SubscribeRequest { Settings = settings });

        await _subscriptionService.Received(1).SubscribeAsync(
            SubscriptionTargetType.Game, gameId, settings, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(SubscriptionTargetType.Topic)]
    [InlineData(SubscriptionTargetType.User)]
    public async Task AskNothingOfGamesForAnotherTarget(SubscriptionTargetType targetType)
    {
        var targetId = Guid.NewGuid();

        await _service.SubscribeAsync(targetType, targetId, null);

        await _gameService.DidNotReceive().GetAsync(Arg.Any<Guid>());
        await _subscriptionService.Received(1).SubscribeAsync(
            targetType, targetId, null, Arg.Any<CancellationToken>());
    }
}
